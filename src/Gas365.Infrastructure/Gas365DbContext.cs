using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class Gas365DbContext(DbContextOptions<Gas365DbContext> options) : DbContext(options);
