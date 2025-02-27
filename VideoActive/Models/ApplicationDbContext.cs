using Microsoft.EntityFrameworkCore;

namespace VideoActive.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}

    }
}
