using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>
/// Uma fábrica só para todas as classes de teste deste projeto — evita corrida na variável de
/// ambiente <c>Redis__ConnectionString</c> (ver <see cref="NornApiFactory"/>) entre coleções
/// paralelas e evita subir dois pares de containers Postgres/Redis à toa.
/// </summary>
[CollectionDefinition(nameof(NornApiCollectionDefinition))]
public sealed class NornApiCollectionDefinition : ICollectionFixture<NornApiFactory>;
