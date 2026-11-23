using System.Net;
using System.Net.Mail;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<PlacementStore>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();

app.MapGet("/api/summary", (PlacementStore s) =>
    s.Summary());

app.MapGet("/api/students", (PlacementStore s) =>
    s.Students);

app.MapPost("/api/email", async (
    EmailRequest req,
    PlacementStore s,
    IConfiguration cfg,
    ILogger<Program> log) =>
{
    var targets = s.Students
        .Where(x => req.StudentIds.Contains(x.Id))
        .ToList();

    if (targets.Count == 0)
    {
        return Results.BadRequest(new
        {
            message = "Select at least one student."
        });
    }

    if (string.IsNullOrWhiteSpace(req.Subject) ||
        string.IsNullOrWhiteSpace(req.Body))
    {
        return Results.BadRequest(new
        {
            message = "Subject and message are required."
        });
    }

    var host = cfg["Smtp:Host"];

    // Demo mode when SMTP is not configured
    if (string.IsNullOrWhiteSpace(host))
    {
        foreach (var student in targets)
        {
            log.LogInformation(
                "[DEMO EMAIL] To {Email}: {Subject}",
                student.Email,
                req.Subject);
        }

        return Results.Ok(new
        {
            sent = targets.Count,
            demo = true
        });
    }

    using var client = new SmtpClient(
        host,
        cfg.GetValue("Smtp:Port", 587))
    {
        EnableSsl = true,
        Credentials = new NetworkCredential(
            cfg["Smtp:User"],
            cfg["Smtp:Password"])
    };

    foreach (var student in targets)
    {
        var body = req.Body.Replace(
            "{name}", student.Name);

        using var message = new MailMessage(
            cfg["Smtp:From"]!,
            student.Email,
            req.Subject,
            body);

        await client.SendMailAsync(message);
    }

    return Results.Ok(new
    {
        sent = targets.Count,
        demo = false
    });
});

app.Run();

record Student(
    int Id,
    string Name,
    string Email,
    string Department,
    double Cgpa,
    string Status,
    string? Company,
    double? PackageLpa);

record EmailRequest(
    List<int> StudentIds,
    string Subject,
    string Body);

record Drive(
    string Company,
    string Date,
    string Status,
    int Offers);

class PlacementStore
{
    public List<Student> Students { get; }
    public List<Drive> Drives { get; }

    public PlacementStore()
    {
        var rnd = new Random(42);

        string[] departments =
        {
            "CSE", "IT", "ECE", "Mechanical", "Civil"
        };

        string[] companies =
        {
            "TCS", "Infosys", "Wipro",
            "Accenture", "Zoho", "L&T", "Bosch"
        };

        string[] firstNames =
        {
            "Aarav", "Diya", "Rohan", "Meera",
            "Kabir", "Isha", "Vihaan", "Anaya",
            "Arjun", "Riya", "Dev", "Nisha",
            "Karan", "Pooja", "Yash", "Sneha",
            "Harsh", "Tara", "Om", "Kavya"
        };

        string[] lastNames =
        {
            "Patel", "Shah", "Mehta", "Joshi",
            "Desai", "Trivedi", "Vyas", "Rana"
        };

        Students = Enumerable.Range(1, 120)
            .Select(i =>
            {
                var name =
                    $"{firstNames[rnd.Next(firstNames.Length)]} " +
                    $"{lastNames[rnd.Next(lastNames.Length)]}";

                var department =
                    departments[rnd.Next(departments.Length)];

                var cgpa = Math.Round(
                    6 + rnd.NextDouble() * 3.8, 2);

                var placed = rnd.NextDouble() <
                    (cgpa > 7.5 ? 0.8 : 0.45);

                return new Student(
                    i,
                    name,
                    $"{name.Replace(' ', '.').ToLower()}{i}@college.edu",
                    department,
                    cgpa,
                    placed ? "Placed" : "Unplaced",
                    placed
                        ? companies[rnd.Next(companies.Length)]
                        : null,
                    placed
                        ? Math.Round(3 + rnd.NextDouble() * 12, 1)
                        : null);
            })
            .ToList();

        Drives = new()
        {
            new Drive("Zoho", "2026-11-04", "Upcoming", 0),
            new Drive("Accenture", "2026-11-12", "Upcoming", 0),
            new Drive("Bosch", "2026-11-19", "Upcoming", 0),
            new Drive(
                "TCS", "2026-09-15", "Completed",
                Students.Count(s => s.Company == "TCS")),
            new Drive(
                "Infosys", "2026-09-28", "Completed",
                Students.Count(s => s.Company == "Infosys"))
        };
    }

    public object Summary()
    {
        var placedStudents = Students
            .Where(s => s.Status == "Placed")
            .ToList();

        return new
        {
            total = Students.Count,
            placed = placedStudents.Count,
            unplaced = Students.Count - placedStudents.Count,

            percent = Math.Round(
                100.0 * placedStudents.Count / Students.Count, 1),

            avgPackage = placedStudents.Count > 0
                ? Math.Round(
                    placedStudents.Average(
                        s => s.PackageLpa!.Value), 1)
                : 0,

            maxPackage = placedStudents.Count > 0
                ? placedStudents.Max(
                    s => s.PackageLpa!.Value)
                : 0,

            byDepartment = Students
                .GroupBy(s => s.Department)
                .Select(g => new
                {
                    department = g.Key,
                    placed = g.Count(
                        x => x.Status == "Placed"),
                    unplaced = g.Count(
                        x => x.Status != "Placed")
                }),

            byCompany = placedStudents
                .GroupBy(s => s.Company)
                .Select(g => new
                {
                    company = g.Key,
                    offers = g.Count()
                })
                .OrderByDescending(x => x.offers),

            packageBands = new[]
            {
                new
                {
                    band = "3–6 LPA",
                    count = placedStudents.Count(
                        s => s.PackageLpa < 6)
                },
                new
                {
                    band = "6–9 LPA",
                    count = placedStudents.Count(
                        s => s.PackageLpa >= 6 &&
                             s.PackageLpa < 9)
                },
                new
                {
                    band = "9–12 LPA",
                    count = placedStudents.Count(
                        s => s.PackageLpa >= 9 &&
                             s.PackageLpa < 12)
                },
                new
                {
                    band = "12+ LPA",
                    count = placedStudents.Count(
                        s => s.PackageLpa >= 12)
                }
            },

            drives = Drives
        };
    }
}
