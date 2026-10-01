using Ampere.Core.Parametros;
using Ampere.Core.Quadros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Quadros;

/// <summary>
///     Porta <see cref="IDocumentoDeQuadros" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     Leitura por quadro: os circuitos são os <c>ElectricalSystem</c> de força atribuídos ao painel; a potência do
///     circuito é a soma de <c>AMP_PotenciaInstaladaVA</c> dos membros (o sistema nativo não guarda a nossa potência);
///     esquema e tensão vêm do primeiro membro classificado que os tiver. AMP_TensaoCircuitoV é número puro.
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

    public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria)
    {
        var painel = documento.GetElement(new ElementId(quadroId)) as FamilyInstance
                     ?? throw new InvalidOperationException($"O quadro {quadroId} não existe no documento.");
        // Quadro em grupo de modelo (ou vínculo): o parâmetro não é editável fora do grupo — só é problema se o valor
        // gravado não é o que deveria estar.
        if (ParametrosAmpere.Ler(painel, ParametrosAmpere.MemoriaCalculoId) is { IsReadOnly: true } somenteLeitura)
            return string.Equals(somenteLeitura.AsString() ?? string.Empty, hashDaMemoria ?? string.Empty, StringComparison.Ordinal);

        ParametrosAmpere.GravarTextoOuApagar(painel, ParametrosAmpere.MemoriaCalculoId, hashDaMemoria);
        return true;
    }

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

    public string CriarTabelaDoQuadro(string nomeDoQuadro)
    {
        var nome = $"{nomeDoQuadro} — quadro de cargas (Ampere)";
        foreach (var existente in new FilteredElementCollector(documento).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                     .Where(tabela => tabela.Name == nome).ToList())
            documento.Delete(existente.Id);

        var tabela = ViewSchedule.CreateSchedule(documento, new ElementId(BuiltInCategory.OST_ElectricalCircuit));
        tabela.Name = nome;
        var definicao = tabela.Definition;

        var campo = Campo(definicao, ParametrosAmpere.NumeroCircuito, "Nº");
        definicao.AddSortGroupField(new ScheduleSortGroupField(campo.FieldId, ScheduleSortOrder.Ascending));
        Campo(definicao, ParametrosAmpere.TipoCarga, "Tipo de carga");
        Campo(definicao, ParametrosAmpere.PotenciaInstaladaVA, "Potência instalada (VA)");
        Campo(definicao, ParametrosAmpere.FatorDemanda, "Fator de demanda");
        Campo(definicao, ParametrosAmpere.MemoriaCalculoId, "Memória do circuito");

        var doQuadro = Campo(definicao, ParametrosAmpere.Quadro, "Quadro");
        doQuadro.IsHidden = true;
        definicao.AddFilter(new ScheduleFilter(doQuadro.FieldId, ScheduleFilterType.Equal, nomeDoQuadro));

        return nome;
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

    public IReadOnlyList<QuadroLido> LerQuadrosComCircuitos() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(painel => (Painel: painel, Sistemas: SistemasDeForca(painel)))
            .Where(par => par.Sistemas.Count > 0)
            .Select(par => new QuadroLido(
                par.Painel.Id.Value,
                NomeDoPainel(par.Painel),
                par.Sistemas.Select(LerCircuito).ToList()))
            .ToList();

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
