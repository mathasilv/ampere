using System.IO;
using Ampere.Core.Parametros;

namespace Ampere.Revit.Parametros;

/// <summary>
///     Porta <see cref="IParametrosDoDocumento" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     Comportamentos conferidos na documentação oficial (RevitAPI.xml 2025/2027):
///     <list type="bullet">
///         <item><c>BindingMap.Insert</c> "posta um erro e retorna false" se o vínculo já existe — o planejador evita;
///         aqui, false vira exceção.</item>
///         <item><c>BindingMap.ReInsert</c> "remove o vínculo e cria um novo" — por isso o novo conjunto de categorias
///         parte das já vinculadas.</item>
///         <item><c>SharedParametersFilename</c> é configuração do usuário — a troca temporária sempre restaura o
///         original.</item>
///     </list>
/// </remarks>
public sealed class ParametrosDoDocumentoRevit(Document documento) : IParametrosDoDocumento
{
    public IReadOnlyCollection<ParametroExistente> LerExistentes()
    {
        var vinculos = new Dictionary<ElementId, VinculoExistente>();
        var iterador = documento.ParameterBindings.ForwardIterator();
        while (iterador.MoveNext())
        {
            if (iterador.Key is InternalDefinition definicao && iterador.Current is ElementBinding vinculo)
                vinculos[definicao.Id] = new VinculoExistente(vinculo is InstanceBinding, CategoriasAmpere(vinculo.Categories));
        }

        // ParameterElement não tem categoria: OfClass é o filtro rápido aplicável (inclui SharedParameterElement).
        return new FilteredElementCollector(documento)
            .OfClass(typeof(ParameterElement))
            .Cast<ParameterElement>()
            .Select(parametro =>
            {
                var definicao = parametro.GetDefinition();
                return new ParametroExistente(
                    definicao.Name,
                    (parametro as SharedParameterElement)?.GuidValue,
                    MapeamentoRevit.Tipo(definicao.GetDataType()),
                    vinculos.GetValueOrDefault(parametro.Id));
            })
            .ToList();
    }

    public void EmUmaTransacao(string nome, Action acao)
    {
        using var transacao = new Transaction(documento, nome);
        transacao.Start();
        acao();
        var situacao = transacao.Commit();
        if (situacao != TransactionStatus.Committed)
            throw new InvalidOperationException($"O Revit não confirmou a transação '{nome}' ({situacao}).");
    }

    public void Criar(IReadOnlyList<DefinicaoDeParametro> definicoes, string grupoRevit)
    {
        var aplicacao = documento.Application;
        var arquivoOriginal = aplicacao.SharedParametersFilename;
        var arquivoTemporario = Path.Combine(Path.GetTempPath(), $"Ampere-parametros-{Guid.NewGuid():N}.txt");
        File.WriteAllText(arquivoTemporario, string.Empty);
        try
        {
            aplicacao.SharedParametersFilename = arquivoTemporario;
            var arquivo = aplicacao.OpenSharedParameterFile()
                          ?? throw new InvalidOperationException("O Revit não abriu o arquivo temporário de parâmetros compartilhados.");
            var grupo = arquivo.Groups.Create(grupoRevit);

            foreach (var definicao in definicoes)
            {
                var opcoes = new ExternalDefinitionCreationOptions(definicao.Nome, MapeamentoRevit.Especificacao(definicao.Tipo))
                {
                    GUID = definicao.Guid,
                    Description = definicao.Descricao,
                    UserModifiable = true,
                    Visible = true
                };
                var externa = grupo.Definitions.Create(opcoes)
                              ?? throw new InvalidOperationException($"O Revit não criou a definição de {definicao.Nome}.");

                var vinculo = new InstanceBinding(ConjuntoDeCategorias(definicao.Categorias));
                if (!documento.ParameterBindings.Insert(externa, vinculo, GroupTypeId.Electrical))
                    throw new InvalidOperationException($"O Revit recusou o vínculo de {definicao.Nome}.");
            }
        }
        finally
        {
            aplicacao.SharedParametersFilename = arquivoOriginal;
            File.Delete(arquivoTemporario);
        }
    }

    public void AmpliarCategorias(DefinicaoDeParametro definicao, IReadOnlyList<CategoriaEletrica> faltantes)
    {
        var elemento = SharedParameterElement.Lookup(documento, definicao.Guid)
                       ?? throw new InvalidOperationException($"{definicao.Nome} não existe no documento.");
        var interna = elemento.GetDefinition();
        var atual = documento.ParameterBindings.get_Item(interna) as ElementBinding
                    ?? throw new InvalidOperationException($"{definicao.Nome} não está vinculado.");

        // Parte do vínculo atual inteiro (inclusive categorias alheias ao Ampere): ampliar nunca remove.
        var categorias = documento.Application.Create.NewCategorySet();
        foreach (Category categoria in atual.Categories) categorias.Insert(categoria);
        foreach (var faltante in faltantes) categorias.Insert(CategoriaRevit(faltante));

        if (!documento.ParameterBindings.ReInsert(interna, new InstanceBinding(categorias), interna.GetGroupTypeId()))
            throw new InvalidOperationException($"O Revit recusou ampliar as categorias de {definicao.Nome}.");
    }

    private CategorySet ConjuntoDeCategorias(IEnumerable<CategoriaEletrica> categorias)
    {
        var conjunto = documento.Application.Create.NewCategorySet();
        foreach (var categoria in categorias) conjunto.Insert(CategoriaRevit(categoria));
        return conjunto;
    }

    private Category CategoriaRevit(CategoriaEletrica categoria)
    {
        var revit = Category.GetCategory(documento, MapeamentoRevit.Categoria(categoria))
                    ?? throw new InvalidOperationException($"Categoria {categoria} não encontrada no documento.");
        if (!revit.AllowsBoundParameters)
            throw new InvalidOperationException($"A categoria {revit.Name} não aceita parâmetros vinculados.");
        return revit;
    }

    private static List<CategoriaEletrica> CategoriasAmpere(CategorySet categorias) =>
        categorias.Cast<Category>()
            .Select(MapeamentoRevit.CategoriaAmpere)
            .OfType<CategoriaEletrica>()
            .ToList();
}
