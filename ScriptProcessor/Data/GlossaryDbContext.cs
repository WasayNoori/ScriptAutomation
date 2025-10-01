using Microsoft.EntityFrameworkCore;
using ScriptProcessor.Models;

namespace ScriptProcessor.Data
{
    public class GlossaryDbContext:DbContext
    {

        public GlossaryDbContext(DbContextOptions<GlossaryDbContext> options)
            :base(options) 
        {
            
        }

        public DbSet<TermTranslation> TermTranslations { get; set; }
    }
}
