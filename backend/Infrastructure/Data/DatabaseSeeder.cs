using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Data;

public static class DatabaseSeeder
{
    public static void Seed(AppDbContext context, Microsoft.Extensions.Configuration.IConfiguration? config = null)
    {
        try
        {
            string adminUsername = Environment.GetEnvironmentVariable("ADMIN_SEED_USERNAME") 
                ?? config?["AdminSeed:Username"] 
                ?? "Sutiksn A";

            string adminFullName = Environment.GetEnvironmentVariable("ADMIN_SEED_FULLNAME") 
                ?? config?["AdminSeed:FullName"] 
                ?? adminUsername;

            string adminEmail = Environment.GetEnvironmentVariable("ADMIN_SEED_EMAIL") 
                ?? config?["AdminSeed:Email"] 
                ?? "**********@gmail.com";

            string adminPassword = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD") 
                ?? config?["AdminSeed:Password"] 
                ?? "******";

            CreatePasswordHash(adminPassword, out string adminHash, out string adminSalt);

            // 1. Seed or Update Platform Admin
            var adminUser = context.Users.FirstOrDefault(u => 
                u.Username.ToLower() == adminUsername.ToLower() || 
                u.Email.ToLower() == adminEmail.ToLower() ||
                u.FullName.ToLower() == adminFullName.ToLower() ||
                u.Role == "PlatformAdmin");

            if (adminUser == null)
            {
                adminUser = new User
                {
                    Username = adminUsername,
                    Email = adminEmail,
                    FullName = adminFullName,
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Role = "PlatformAdmin",
                    Department = "Executive Leadership",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    IsActive = true
                };
                context.Users.Add(adminUser);
                context.SaveChanges();
                Console.WriteLine($"[Seeder] Created Platform Admin {adminUser.FullName} (id={adminUser.Id})");
            }
            else
            {
                adminUser.Username = adminUsername;
                adminUser.FullName = adminFullName;
                adminUser.Email = adminEmail;
                adminUser.Role = "PlatformAdmin";
                adminUser.PasswordHash = adminHash;
                adminUser.PasswordSalt = adminSalt;
                adminUser.IsActive = true;
                context.SaveChanges();
                Console.WriteLine($"[Seeder] Synchronized Platform Admin {adminUser.FullName} (id={adminUser.Id})");
            }

            // 2. Ensure Platform Admin has at least one Personal workspace
            bool hasPersonalWs = context.Workspaces.Any(w => w.CreatedByUserId == adminUser.Id && w.Mode == "Personal");
            if (!hasPersonalWs)
            {
                var adminWs = new Workspace
                {
                    Name = "Personal Workspace",
                    Description = "Private productivity, daily tasks, and focus goals",
                    Mode = "Personal",
                    Color = "#4f46e5",
                    CreatedByUserId = adminUser.Id,
                    CreatedAt = DateTime.UtcNow
                };
                context.Workspaces.Add(adminWs);
                context.SaveChanges();

                context.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = adminWs.Id,
                    UserId = adminUser.Id,
                    Role = "Admin",
                    JoinedAt = DateTime.UtcNow
                });
                context.SaveChanges();
                Console.WriteLine($"[Seeder] Seeded default Personal workspace for Sutiksn A");
            }

            // 3. Migrate legacy OrgOwner role to OrgAdmin
            var orgOwnerUsers = context.Users.Where(u => u.Role == "OrgOwner").ToList();
            foreach (var u in orgOwnerUsers) { u.Role = "OrgAdmin"; }

            var orgOwnerMembers = context.OrganizationMembers.Where(m => m.Role == "OrgOwner").ToList();
            foreach (var m in orgOwnerMembers) { m.Role = "OrgAdmin"; }

            var orgOwnerReqs = context.AccessRequests.Where(r => r.RequestedRole == "OrgOwner").ToList();
            foreach (var r in orgOwnerReqs) { r.RequestedRole = "OrgAdmin"; }

            context.SaveChanges();

            // 4. Organization Deduplication (merge duplicate organizations by name)
            var allOrgs = context.Organizations
                .Include(o => o.Members)
                .Include(o => o.Workspaces)
                    .ThenInclude(w => w.Members)
                .Include(o => o.Workspaces)
                    .ThenInclude(w => w.Tasks)
                .Include(o => o.Workspaces)
                    .ThenInclude(w => w.Projects)
                .Include(o => o.Workspaces)
                    .ThenInclude(w => w.Goals)
                .AsSplitQuery()
                .ToList();

            var orgGroups = allOrgs.GroupBy(o => o.Name.Trim().ToLower()).Where(g => g.Count() > 1).ToList();
            foreach (var group in orgGroups)
            {
                var primaryOrg = group.OrderBy(o => o.Id).First();
                var duplicateOrgs = group.Where(o => o.Id != primaryOrg.Id).ToList();

                foreach (var dupOrg in duplicateOrgs)
                {
                    // Merge members
                    foreach (var member in dupOrg.Members.ToList())
                    {
                        if (!primaryOrg.Members.Any(m => m.UserId == member.UserId))
                        {
                            member.OrganizationId = primaryOrg.Id;
                        }
                        else
                        {
                            context.OrganizationMembers.Remove(member);
                        }
                    }

                    // Merge or reassign workspaces
                    var primaryCollabWs = primaryOrg.Workspaces.FirstOrDefault(w => w.Mode == "Business" || w.Mode == "Team");
                    foreach (var dupWs in dupOrg.Workspaces.ToList())
                    {
                        if (primaryCollabWs != null && (dupWs.Mode == "Business" || dupWs.Mode == "Team"))
                        {
                            foreach (var wm in dupWs.Members.ToList())
                            {
                                if (!primaryCollabWs.Members.Any(m => m.UserId == wm.UserId))
                                {
                                    wm.WorkspaceId = primaryCollabWs.Id;
                                }
                                else
                                {
                                    context.WorkspaceMembers.Remove(wm);
                                }
                            }

                            foreach (var task in dupWs.Tasks.ToList())
                            {
                                task.WorkspaceId = primaryCollabWs.Id;
                            }

                            foreach (var proj in dupWs.Projects.ToList())
                            {
                                proj.WorkspaceId = primaryCollabWs.Id;
                            }

                            foreach (var goal in dupWs.Goals.ToList())
                            {
                                goal.WorkspaceId = primaryCollabWs.Id;
                            }

                            context.Workspaces.Remove(dupWs);
                        }
                        else
                        {
                            dupWs.OrganizationId = primaryOrg.Id;
                        }
                    }

                    var reqs = context.AccessRequests.Where(r => r.OrganizationId == dupOrg.Id).ToList();
                    foreach (var r in reqs) r.OrganizationId = primaryOrg.Id;

                    var acts = context.ActivityLogs.Where(a => a.OrganizationId == dupOrg.Id).ToList();
                    foreach (var a in acts) a.OrganizationId = primaryOrg.Id;

                    context.Organizations.Remove(dupOrg);
                }
                context.SaveChanges();
                Console.WriteLine($"[Seeder] Deduplicated organizations for '{primaryOrg.Name}'");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Database Note] Seeder execution note: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32
        );

        passwordSalt = Convert.ToBase64String(salt);
        passwordHash = Convert.ToBase64String(hash);
    }
}
