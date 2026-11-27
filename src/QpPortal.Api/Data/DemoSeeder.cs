using Microsoft.EntityFrameworkCore;

namespace QpPortal.Api.Data;

public static class DemoSeeder
{
    public static async Task SeedAsync(AppDb db)
    {
        if (await db.Users.AnyAsync()) return;

        var roles = await db.Roles.ToDictionaryAsync(r => r.Name);
        var hash = BCrypt.Net.BCrypt.HashPassword("Demo@123");

        void Staff(string name, string email, string dept, string role) =>
            db.Users.Add(new User
            {
                Name = name, Email = email, Department = dept, PasswordHash = hash, Source = "Seed",
                UserRoles = [new UserRole { RoleId = roles[role].Id }]
            });

        Staff("Dr. CoE",  "coe@qp.local",     "Exam Cell", "CoE");
        Staff("Dr. DCoE", "dcoe@qp.local",    "Exam Cell", "DCoE");
        Staff("Dr. HoD",  "hod.cse@qp.local", "CSE",       "HoD");

        db.Users.Add(new User
        {
            Name = "Prof. Demo Setter", Email = "setter1@qp.local", Mobile = "9876543210",
            DateOfBirth = new DateOnly(1985, 6, 15), Designation = "Associate Professor",
            Department = "CSE", Institution = "Demo College", SetterType = "Internal",
            ExperienceYears = 12, AreaOfInterest = "algorithms, data structures, graph theory",
            Source = "Seed",
            UserRoles = [new UserRole { RoleId = roles["Setter"].Id }]
        });

        await db.SaveChangesAsync();
    }
}