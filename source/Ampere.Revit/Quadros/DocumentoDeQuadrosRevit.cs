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
        var sistemasPorQuadro = new Dictionary<long, Dictionary<string, ElectricalSystem>>();
        foreach (var grupo in linhas.GroupBy(linha => linha.QuadroId))
        {
            var painel = documento.GetElement(new ElementId(grupo.Key)) as FamilyInstance
                         ?? throw new InvalidOperationException($"O quadro {grupo.Key} não existe no documento.");
            sistemasPorQuadro[grupo.Key] = SistemasDeForca(painel).Where(sistema =>
                    ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito) is { Length: > 0 } numero)
                .ToDictionary(sistema => ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito)!, StringComparer.Ordinal);
        }

        foreach (var linha in linhas)
        {
            if (!sistemasPorQuadro[linha.QuadroId].TryGetValue(linha.NumeroDoCircuito, out var sistema))
                throw new InvalidOperationException($"Circuito {linha.NumeroDoCircuito} não encontrado no quadro para gravar o resultado.");

            ParametrosAmpere.GravarNumero(sistema, ParametrosAmpere.PotenciaInstaladaVA,
                UnitUtils.ConvertToInternalUnits((double)linha.PotenciaVA, UnitTypeId.VoltAmperes));
            if (linha.Fator is { } fator) ParametrosAmpere.GravarNumero(sistema, ParametrosAmpere.FatorDemanda, (double)fator);
            if (linha.HashDaMemoria is { } hash) ParametrosAmpere.GravarTexto(sistema, ParametrosAmpere.MemoriaCalculoId, hash);
        }
    }

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
        Campo(definicao, ParametrosAmpere.MemoriaCalculoId, "Memória de cálculo");

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
