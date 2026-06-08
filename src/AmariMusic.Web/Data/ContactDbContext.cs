using AmariMusic.Models;
using Microsoft.EntityFrameworkCore;

namespace AmariMusic.Data;

public class ContactDbContext(DbContextOptions<ContactDbContext> options) : DbContext(options)
{
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
}
