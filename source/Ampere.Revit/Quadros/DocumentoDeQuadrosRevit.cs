using Ampere.Core.Cargas;
using Ampere.Core.Parametros;
using Ampere.Core.Quadros;
using Ampere.Revit.Armazenamento;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Quadros;

/// <summary>
///     Porta <see cref="IDocumentoDeQuadros" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     Leitura por quadro: os circuitos são os <c>ElectricalSystem</c> de força atribuídos ao painel; a potência do
///     circuito é a soma de <c>AMP_PotenciaInstaladaVA</c> dos membros (o sistema nativo não guarda a nossa potência);
///     esquema e tensão vêm do primeiro membro classificado que os tiver. AMP_TensaoCircuitoV é número puro. A
///     alimentação do quadro vem do sistema de distribuição atribuído ao painel (fases, fios e tensões).
/// </remarks>
public sealed class DocumentoDeQuadrosRevit(Document documento) : IDocumentoDeQuadros
{
    public void EmUmaTransacao(string nome, Action acao) => TransacaoRevit.Executar(documento, nome, acao);

    public void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas)
    {
        foreach (var linha in linhas)
        {
            var sistema = documento.GetElement(new ElementId(linha.CircuitoId)) as ElectricalSystem
                          ?? throw new InvalidOperationException($"Circuito {linha.NumeroDoCircuito} não encontrado no documento para gravar o resultado.");

            ParametrosAmpere.GravarNumeroOuApagar(sistema, ParametrosAmpere.PotenciaInstaladaVA,
                linha.PotenciaVA is { } potencia ? UnitUtils.ConvertToInternalUnits((double)potencia, UnitTypeId.VoltAmperes) : null);
            ParametrosAmpere.GravarNumeroOuApagar(sistema, ParametrosAmpere.FatorDemanda, (double?)linha.Fator);
            ParametrosAmpere.GravarTextoOuApagar(sistema, ParametrosAmpere.Quadro, linha.Quadro);
        }
    }

    private static readonly EsquemaJson FatoresDoQuadro = new(new Guid(FatoresEmJson.GuidDoEsquema), "AmpereFatoresDoQuadro", "FatoresJson",
        "Ampere: fatores de demanda informados na montagem do quadro de cargas (JSON versionado).");

    public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados)
    {
        var painel = Painel(quadroId);
        // Quadro em grupo de modelo (ou vínculo): o parâmetro não é editável fora do grupo — só é problema se o valor
        // gravado não é o que deveria estar. Os fatores vão junto com o hash, ou nenhum dos dois.
        if (ParametrosAmpere.Ler(painel, ParametrosAmpere.MemoriaCalculoId) is { IsReadOnly: true } somenteLeitura)
            return string.Equals(somenteLeitura.AsString() ?? string.Empty, hashDaMemoria ?? string.Empty, StringComparison.Ordinal);

        ParametrosAmpere.GravarTextoOuApagar(painel, ParametrosAmpere.MemoriaCalculoId, hashDaMemoria);
        FatoresDoQuadro.GravarEm(painel, FatoresEmJson.Escrever(fatoresInformados));
        return true;
    }

    public string? LerMemoriaDoQuadro(long quadroId) =>
        ParametrosAmpere.LerTexto(Painel(quadroId), ParametrosAmpere.MemoriaCalculoId) is { Length: > 0 } hash ? hash : null;

    public IReadOnlyDictionary<TipoDeCarga, decimal>? LerFatoresDoQuadro(long quadroId) => FatoresEmJson.Ler(FatoresDoQuadro.LerDo(Painel(quadroId)));

    private FamilyInstance Painel(long quadroId) =>
        documento.GetElement(new ElementId(quadroId)) as FamilyInstance
        ?? throw new InvalidOperationException($"O quadro {quadroId} não existe no documento.");

    public IReadOnlyList<string> ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados)
    {
        var ignorar = montados.ToHashSet();
        var semEdicao = new List<string>();
        foreach (var painel in new FilteredElementCollector(documento)
                     .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                     .OfClass(typeof(FamilyInstance))
                     .Cast<FamilyInstance>()
                     .Where(painel => !ignorar.Contains(painel.Id.Value)))
        {
            if (ParametrosAmpere.Ler(painel, ParametrosAmpere.MemoriaCalculoId) is not { } parametro || string.IsNullOrEmpty(parametro.AsString())) continue;

            if (parametro.IsReadOnly) semEdicao.Add(NomeDoPainel(painel));
            else ParametrosAmpere.GravarTextoOuApagar(painel, ParametrosAmpere.MemoriaCalculoId, null);
        }

        return semEdicao;
    }

    public IReadOnlyList<CircuitoLido> LerCircuitosSemQuadro() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalCircuit)
            .WhereElementIsNotElementType()
            .OfType<ElectricalSystem>()
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit && sistema.BaseEquipment is null)
            .Select(LerCircuito)
            .ToList();

    /// <remarks>
    ///     A tabela que já existe é reaproveitada, nunca apagada: apagar a view tiraria a tabela das pranchas em que o
    ///     projetista a colocou. Com as colunas certas, fica como está (largura e formatação do projetista preservadas);
    ///     com colunas de outra versão do Ampere, os campos são refeitos na mesma view.
    /// </remarks>
    public string CriarTabelaDoQuadro(string nomeDoQuadro)
    {
        var nome = $"{nomeDoQuadro} — quadro de cargas (Ampere)";
        var existente = new FilteredElementCollector(documento).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
            .FirstOrDefault(tabela => tabela.Name == nome);
        if (existente is not null && ColunasAtuais(existente.Definition)) return nome;

        var tabela = existente ?? ViewSchedule.CreateSchedule(documento, new ElementId(BuiltInCategory.OST_ElectricalCircuit));
        if (existente is null) tabela.Name = nome;
        var definicao = tabela.Definition;
        definicao.ClearSortGroupFields();
        definicao.ClearFilters();
        definicao.ClearFields();

        var campo = Campo(definicao, ParametrosAmpere.NumeroCircuito, "Nº");
        definicao.AddSortGroupField(new ScheduleSortGroupField(campo.FieldId, ScheduleSortOrder.Ascending));
        // Descrição = o "Nome da carga" nativo do circuito: o projetista edita no Revit, o Ampere não escreve.
        CampoNativo(definicao, BuiltInParameter.RBS_ELEC_CIRCUIT_NAME, "Descrição");
        Campo(definicao, ParametrosAmpere.TipoCarga, "Tipo de carga");
        Campo(definicao, ParametrosAmpere.PotenciaInstaladaVA, "Potência instalada (VA)");
        Campo(definicao, ParametrosAmpere.FatorDemanda, "Fator de demanda");
        // Resultados do 'Dimensionar circuitos', lidos ao vivo dos parâmetros do circuito.
        Campo(definicao, ParametrosAmpere.CorrenteProjetoA, "IB (A)");
        Campo(definicao, ParametrosAmpere.BitolaCondutorMm2, "Seção (mm²)");
        Campo(definicao, ParametrosAmpere.DisjuntorNominalA, "Disjuntor (A)");
        Campo(definicao, ParametrosAmpere.IdrSensibilidadeMa, "IDR (mA)");
        Campo(definicao, ParametrosAmpere.QuedaTensaoPct, "Queda (%)");
        Campo(definicao, ParametrosAmpere.MemoriaCalculoId, "Memória do circuito");

        var doQuadro = Campo(definicao, ParametrosAmpere.Quadro, "Quadro");
        doQuadro.IsHidden = true;
        definicao.AddFilter(new ScheduleFilter(doQuadro.FieldId, ScheduleFilterType.Equal, nomeDoQuadro));

        return nome;
    }

    private static readonly string[] Colunas =
    [
        "Nº", "Descrição", "Tipo de carga", "Potência instalada (VA)", "Fator de demanda",
        "IB (A)", "Seção (mm²)", "Disjuntor (A)", "IDR (mA)", "Queda (%)", "Memória do circuito", "Quadro"
    ];

    private static bool ColunasAtuais(ScheduleDefinition definicao) =>
        definicao.GetFilterCount() == 1 &&
        definicao.GetSortGroupFieldCount() == 1 &&
        Enumerable.Range(0, definicao.GetFieldCount()).Select(indice => definicao.GetField(indice).ColumnHeading).SequenceEqual(Colunas);

    private static ScheduleField CampoNativo(ScheduleDefinition definicao, BuiltInParameter parametro, string titulo)
    {
        var campoSchedulavel = definicao.GetSchedulableFields()
                                   .FirstOrDefault(campo => campo.FieldType == ScheduleFieldType.Instance && campo.ParameterId == new ElementId(parametro))
                               ?? throw new InvalidOperationException($"O parâmetro nativo {parametro} não aparece como campo de tabela para circuitos.");
        var campo = definicao.AddField(campoSchedulavel);
        campo.ColumnHeading = titulo;
        return campo;
    }

    private ScheduleField Campo(ScheduleDefinition definicao, DefinicaoDeParametro definicaoDoParametro, string titulo)
    {
        var idDoParametro = SharedParameterElement.Lookup(documento, definicaoDoParametro.Guid)?.Id
                            ?? throw new InvalidOperationException($"{definicaoDoParametro.Nome} não existe no documento (rode 'Injetar parâmetros').");
        var campoSchedulavel = definicao.GetSchedulableFields()
            .SingleOrDefault(campo => campo.FieldType == ScheduleFieldType.Instance && campo.ParameterId == idDoParametro)
            ?? throw new InvalidOperationException($"{definicaoDoParametro.Nome} não aparece como campo de tabela para circuitos.");
        var campo = definicao.AddField(campoSchedulavel);
        campo.ColumnHeading = titulo;
        return campo;
    }

    public IReadOnlyList<QuadroLido> LerQuadrosComCircuitos()
    {
        var rotulos = LeituraDoPainel.RotulosDasFases(documento);
        return new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(painel => (Painel: painel, Sistemas: SistemasDeForca(painel)))
            .Where(par => par.Sistemas.Count > 0)
            .Select(par => new QuadroLido(
                par.Painel.Id.Value,
                NomeDoPainel(par.Painel),
                par.Sistemas.Select(sistema => LerCircuito(sistema) with { FasesNoQuadro = LeituraDoPainel.FasesNoQuadro(sistema, rotulos) }).ToList(),
                LeituraDoPainel.Alimentacao(documento, par.Painel, rotulos)))
            .ToList();
    }

    private static List<ElectricalSystem> SistemasDeForca(FamilyInstance painel) =>
        painel.MEPModel.GetAssignedElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .ToList() ?? [];

    private static CircuitoLido LerCircuito(ElectricalSystem sistema)
    {
        var membros = sistema.Elements.Cast<Element>().ToList();
        return new CircuitoLido(
            sistema.Id.Value,
            ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito) is { Length: > 0 } numero ? numero : sistema.Name,
            ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.TipoCarga),
            PotenciaDosMembros(membros),
            PrimeiroTexto(membros, ParametrosAmpere.Fases),
            PrimeiroNumero(membros, ParametrosAmpere.TensaoCircuitoV));
    }

    private static decimal? PotenciaDosMembros(IReadOnlyList<Element> membros)
    {
        decimal? total = null;
        foreach (var membro in membros)
        {
            if (ParametrosAmpere.Ler(membro, ParametrosAmpere.PotenciaInstaladaVA) is not { HasValue: true } parametro) continue;
            var va = (decimal)UnitUtils.ConvertFromInternalUnits(parametro.AsDouble(), UnitTypeId.VoltAmperes);
            total = (total ?? 0m) + va;
        }

        return total;
    }

    private static string? PrimeiroTexto(IReadOnlyList<Element> membros, DefinicaoDeParametro definicao)
    {
        foreach (var membro in membros)
        {
            if (ParametrosAmpere.LerTexto(membro, definicao) is { Length: > 0 } texto) return texto;
        }

        return null;
    }

    private static decimal? PrimeiroNumero(IReadOnlyList<Element> membros, DefinicaoDeParametro definicao)
    {
        foreach (var membro in membros)
        {
            if (ParametrosAmpere.Ler(membro, definicao) is { HasValue: true } parametro) return (decimal)parametro.AsDouble();
        }

        return null;
    }

    private static string NomeDoPainel(FamilyInstance painel) =>
        painel.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } nome ? nome : painel.Name;
}
