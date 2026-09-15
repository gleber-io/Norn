using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Norn.Knowledge;

/// <summary>
/// Fábrica de design-time para <c>dotnet ef migrations</c> — Norn.Knowledge é biblioteca pura
/// (Microsoft.NET.Sdk, sem host próprio), então as ferramentas do EF Core não têm de onde
/// resolver <see cref="KnowledgeDbContext"/> sem isto. Não participa do runtime: em produção a
/// string de conexão vem de <c>KnowledgeServiceCollectionExtensions.AddNornKnowledge</c>.
/// </summary>
public sealed class KnowledgeDbContextFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=norn;Username=norn;Password=norn");

        return new KnowledgeDbContext(optionsBuilder.Options);
    }
}
