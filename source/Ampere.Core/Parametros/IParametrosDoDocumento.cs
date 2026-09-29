namespace Ampere.Core.Parametros;

/// <summary>
///     Porta para os parâmetros de um documento (implementada pelo adapter Revit). Isola as chamadas da API do Revit
///     usadas na injeção para que possam ser trocadas sem tocar na regra.
/// </summary>
public interface IParametrosDoDocumento : IDocumentoTransacional
{
    /// <summary>Retrato dos parâmetros existentes no documento (compartilhados, de projeto e globais).</summary>
    IReadOnlyCollection<ParametroExistente> LerExistentes();

    /// <summary>Cria e vincula por instância, num único lote, as definições informadas.</summary>
    void Criar(IReadOnlyList<DefinicaoDeParametro> definicoes, string grupoRevit);

    /// <summary>Acrescenta categorias ao vínculo existente, preservando as que já estão nele.</summary>
    void AmpliarCategorias(DefinicaoDeParametro definicao, IReadOnlyList<CategoriaEletrica> faltantes);
}
