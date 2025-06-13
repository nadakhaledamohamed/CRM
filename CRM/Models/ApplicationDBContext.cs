//using Microsoft.EntityFrameworkCore;

//namespace CRM.Models
//{
//    public class ApplicationDBContext: DbContext
//    {
//        public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options) : base(options)
//        {
//        }
//        public DbSet<User> Users { get; set; }
//        public DbSet<Person> Persons { get; set; }
//        public DbSet<Request> Requests { get; set; }
//        public DbSet<LookUpStatusType> LookUpStatusTypes { get; set; }
//        public DbSet<StatusHistory> StatusHistories { get; set; }
//        protected override void OnModelCreating(ModelBuilder modelBuilder)
//        {
//            modelBuilder.Entity<User>(entity =>
//            {
//                entity.HasKey(e => e.UserId);
//                entity.ToTable("Users");
//            });
//            modelBuilder.Entity<Person>(entity =>
//            {
//                entity.HasKey(e => e.PersonId);
//                entity.ToTable("Person");
//            });
//            modelBuilder.Entity<Request>(entity =>
//            {
//                entity.HasKey(e => e.RequestId);
//                entity.ToTable("Request");
//            });
//            modelBuilder.Entity<LookUpStatusType>(entity =>
//            {
//                entity.HasKey(e => e.StatusId);
//                entity.ToTable("LookUp_StatusTypes");
//            });
//            modelBuilder.Entity<StatusHistory>(entity =>
//            {
//                entity.HasKey(e => e.HistoryId);
//                entity.ToTable("StatusHistory");
//            });
//        }
//    }
//    {
//    }
//}
