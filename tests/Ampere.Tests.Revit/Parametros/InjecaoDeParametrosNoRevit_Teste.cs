using Ampere.Core.Parametros;
using Ampere.Revit.Parametros;

namespace Ampere.Tests.Revit.Parametros;

/// <summary>
///     Injeção de parâmetros contra o Revit real.
/// </summary>
/// <remarks>
///     As verificações usam a API do Revit diretamente, com mapeamentos escritos aqui — independentes do adapter —
///     para não testar o código contra ele mesmo. Roda depois de <see cref="DesempenhoDaInjecao_Teste" />, que precisa
///     medir a primeira injeção da sessão.
/// </remarks>
[DependsOn(typeof(DesempenhoDaInjecao_Teste), ProceedOnFailure = true)]
public sealed class InjecaoDeParametrosNoRevit_Teste : TesteComProjetoNovo
{
    private static readonly Guid GuidAlheio = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static readonly Dictionary<CategoriaEletrica, BuiltInCategory> CategoriaEsperada = new()
    {
        [CategoriaEletrica.Luminarias] = BuiltInCategory.OST_LightingFixtures,
        [CategoriaEletrica.DispositivosDeIluminacao] = BuiltInCategory.OST_LightingDevices,
        [CategoriaEletrica.DispositivosEletricos] = BuiltInCategory.OST_ElectricalFixtures,
        [CategoriaEletrica.EquipamentosMecanicos] = BuiltInCategory.OST_MechanicalEquipment,
        [CategoriaEletrica.EquipamentosEletricos] = BuiltInCategory.OST_ElectricalEquipment,
        [CategoriaEletrica.CircuitosEletricos] = BuiltInCategory.OST_ElectricalCircuit
    };

    private static readonly Dictionary<TipoDeDadoDoParametro, ForgeTypeId> EspecificacaoEsperada = new()
    {
        [TipoDeDadoDoParametro.Texto] = SpecTypeId.String.Text,
        [TipoDeDadoDoParametro.Numero] = SpecTypeId.Number,
        [TipoDeDadoDoParametro.PotenciaAparente] = SpecTypeId.ApparentPower,
        [TipoDeDadoDoParametro.Corrente] = SpecTypeId.Current,
        [TipoDeDadoDoParametro.Comprimento] = SpecTypeId.Length
    };

    private static CatalogoDeParametros Catalogo => CatalogoDeParametros.Padrao;

    [Test]
    public async Task Projeto_limpo_recebe_todos_os_parametros_com_GUID_tipo_e_categorias_do_catalogo()
    {
        var plano = Injetar();

        await Assert.That(plano.Acoes.All(acao => acao is AcaoDeInjecao.Criar)).IsTrue();

        var problemas = new List<string>();
        foreach (var definicao in Catalogo.Parametros)
        {
            var elemento = SharedParameterElement.Lookup(Documento, definicao.Guid);
            if (elemento is null)
            {
                problemas.Add($"{definicao.Nome}: não existe no projeto");
                continue;
            }

            var interna = elemento.GetDefinition();
            if (interna.Name != definicao.Nome) problemas.Add($"{definicao.Nome}: nome no Revit '{interna.Name}'");
            if (interna.GetDataType() != EspecificacaoEsperada[definicao.Tipo])
                problemas.Add($"{definicao.Nome}: tipo no Revit '{interna.GetDataType().TypeId}'");

            if (Documento.ParameterBindings.get_Item(interna) is not InstanceBinding vinculo)
            {
                problemas.Add($"{definicao.Nome}: não está vinculado por instância");
                continue;
            }

            var atuais = CategoriasDe(vinculo);
            var esperadas = definicao.Categorias.Select(categoria => (long)CategoriaEsperada[categoria]).ToHashSet();
            if (!atuais.SetEquals(esperadas))
                problemas.Add($"{definicao.Nome}: categorias [{string.Join(", ", atuais)}], esperado [{string.Join(", ", esperadas)}]");
        }

        await Assert.That(problemas).IsEmpty();
    }

    [Test]
    public async Task Injetar_duas_vezes_seguidas_nao_duplica_nem_altera_nada()
    {
        Injetar();
        var segunda = Injetar();

        await Assert.That(segunda.NadaAFazer).IsTrue();
        var nomesAmpere = NomesDosParametrosCompartilhados().Where(nome => nome.StartsWith("AMP_", StringComparison.Ordinal)).ToList();
        await Assert.That(nomesAmpere.Count).IsEqualTo(Catalogo.Parametros.Count);
        await Assert.That(nomesAmpere.Distinct().Count()).IsEqualTo(Catalogo.Parametros.Count);
    }

    [Test]
    public async Task Ampliar_categorias_preserva_valores_e_categorias_alheias_ao_Ampere()
    {
        var tipoCarga = Catalogo.Parametros.Single(parametro => parametro.Nome == "AMP_TipoCarga");
        VincularSoAsInformacoesDoProjeto(tipoCarga.Guid, tipoCarga.Nome);
        Transacionar(() => Documento.ProjectInformation.get_Parameter(tipoCarga.Guid).Set("TUG"));

        var plano = Injetar();

        await Assert.That(plano.Acoes.Single(acao => acao.Definicao == tipoCarga)).IsTypeOf<AcaoDeInjecao.AmpliarCategorias>();
        await Assert.That(Documento.ProjectInformation.get_Parameter(tipoCarga.Guid).AsString()).IsEqualTo("TUG");

        var vinculo = (ElementBinding)Documento.ParameterBindings.get_Item(SharedParameterElement.Lookup(Documento, tipoCarga.Guid).GetDefinition());
        var esperadas = tipoCarga.Categorias.Select(categoria => (long)CategoriaEsperada[categoria])
            .Append((long)BuiltInCategory.OST_ProjectInformation)
            .ToHashSet();
        await Assert.That(CategoriasDe(vinculo).SetEquals(esperadas)).IsTrue();
    }

    [Test]
    public async Task Homonimo_com_outro_GUID_aborta_sem_alterar_o_projeto()
    {
        VincularSoAsInformacoesDoProjeto(GuidAlheio, "AMP_TipoCarga");
        var antes = NomesDosParametrosCompartilhados();

        var plano = Injetar();

        await Assert.That(plano.TemConflitos).IsTrue();
        await Assert.That(NomesDosParametrosCompartilhados()).IsEquivalentTo(antes);
        await Assert.That(SharedParameterElement.Lookup(Documento, Catalogo.Parametros[0].Guid)).IsNull();
    }

    [Test]
    public async Task Arquivo_de_parametros_compartilhados_do_usuario_e_restaurado()
    {
        var original = Application.SharedParametersFilename;
        var doUsuario = Path.Combine(Path.GetTempPath(), $"Ampere-usuario-{Guid.NewGuid():N}.txt");
        File.WriteAllText(doUsuario, string.Empty);
        try
        {
            Application.SharedParametersFilename = doUsuario;

            Injetar();

            await Assert.That(Application.SharedParametersFilename).IsEqualTo(doUsuario);
        }
        finally
        {
            Application.SharedParametersFilename = original;
            File.Delete(doUsuario);
        }
    }

    private PlanoDeInjecao Injetar() => InjecaoDeParametros.Executar(Catalogo, new ParametrosDoDocumentoRevit(Documento));

    private List<string> NomesDosParametrosCompartilhados() =>
        new FilteredElementCollector(Documento)
            .OfClass(typeof(SharedParameterElement))
            .Cast<SharedParameterElement>()
            .Select(parametro => parametro.GetDefinition().Name)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static HashSet<long> CategoriasDe(ElementBinding vinculo) =>
        vinculo.Categories.Cast<Category>().Select(categoria => categoria.Id.Value).ToHashSet();

    /// <summary>Cria um parâmetro compartilhado de texto vinculado só a Informações do projeto (cenário de partida).</summary>
    private void VincularSoAsInformacoesDoProjeto(Guid guid, string nome)
    {
        var original = Application.SharedParametersFilename;
        var arquivo = Path.Combine(Path.GetTempPath(), $"Ampere-teste-{Guid.NewGuid():N}.txt");
        File.WriteAllText(arquivo, string.Empty);
        try
        {
            Application.SharedParametersFilename = arquivo;
            var grupo = Application.OpenSharedParameterFile().Groups.Create("Teste");
            var definicao = grupo.Definitions.Create(new ExternalDefinitionCreationOptions(nome, SpecTypeId.String.Text) { GUID = guid });
            var categorias = Application.Create.NewCategorySet();
            categorias.Insert(Category.GetCategory(Documento, BuiltInCategory.OST_ProjectInformation));
            Transacionar(() => Documento.ParameterBindings.Insert(definicao, new InstanceBinding(categorias), GroupTypeId.Electrical));
        }
        finally
        {
            Application.SharedParametersFilename = original;
            File.Delete(arquivo);
        }
    }

    private void Transacionar(Action acao)
    {
        using var transacao = new Transaction(Documento, "Preparação do teste");
        transacao.Start();
        acao();
        transacao.Commit();
    }
}
