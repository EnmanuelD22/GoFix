using GoFix.Application.Modules.Reviews;

namespace GoFix.Application.Modules.Professionals;

public sealed class DirectoryService(IProfessionalDirectory directory, IReviewQuery reviews)
{
    #region Consultas públicas

    public async Task<ProfessionalPage> SearchAsync(DirectorySearch query, CancellationToken cancellationToken)
    {
        var page = await directory.SearchAsync(query, cancellationToken);
        var reputations = await reviews.GetReputationsAsync(page.Items.Select(item => item.Id).ToArray(), cancellationToken);
        return page with { Items = page.Items.Select(item => AddReputation(item, reputations)).ToArray() };
    }

    public async Task<PublicProfessional?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await directory.GetAsync(id, cancellationToken);
        if (item is null)
            return null;
        var reputations = await reviews.GetReputationsAsync([item.Id], cancellationToken);
        return AddReputation(item, reputations);
    }

    #endregion

    public static Dictionary<string, string[]> Validate(DirectorySearch query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1)
            errors["page"] = ["La página debe ser mayor o igual a 1."];
        if (query.PageSize is < 1 or > 50)
            errors["pageSize"] = ["El tamaño de página debe estar entre 1 y 50."];
        // SQL Server y LINQ usan desplazamiento int. Validar antes de multiplicar evita desbordamientos.
        if (query.Page > 0 && query.PageSize > 0 && (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            errors["page"] = ["La página solicitada excede el rango admitido."];
        if (query.EspecialidadId is <= 0)
            errors["especialidadId"] = ["La especialidad debe ser un identificador positivo."];
        if (query.SectorId is <= 0)
            errors["sectorId"] = ["El sector debe ser un identificador positivo."];
        return errors;
    }

    private static PublicProfessional AddReputation(PublicProfessional item, IReadOnlyDictionary<Guid, Reputation> reputations) =>
        reputations.TryGetValue(item.Id, out var reputation)
            ? item with { Promedio = reputation.Promedio, CantidadValoraciones = reputation.Cantidad }
            : item;
}
