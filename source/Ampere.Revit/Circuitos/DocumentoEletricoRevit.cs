using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Circuitos;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Circuitos;

/// <summary>
///     Porta <see cref="IDocumentoEletrico" /> sobre a API do Revit.
/// </summary>
/// <remarks>
///     Comportamentos conferidos em spike no Revit 2027 (template elétrico PTB) e na documentação oficial:
///     <list type="bullet">
///         <item>Tensão, polos e carga nativos vêm dos parâmetros do conector (<c>MEPFamilyConnectorInfo</c>) — é o que
///         o Revit compara para aceitar pontos no mesmo circuito.</item>
///         <item><c>ElectricalSystem.Create</c> recusa elemento já circuitado ou incompatível com exceção; <c>SelectPanel</c>
///         falha se o quadro não tiver sistema de distribuição compatível. Qualquer falha aborta a transação inteira.</item>
///         <item>AMP_PotenciaInstaladaVA é potência aparente: o Revit guarda em unidades internas, convertidas por
///         <c>UnitUtils</c>; AMP_TensaoCircuitoV e AMP_FatorPotencia são números puros.</item>
///     </list>
/// </remarks>
public sealed class DocumentoEletricoRevit(Document documento) : IDocumentoEletrico
{
    public void EmUmaTransacao(string nome, Action acao) => TransacaoRevit.Executar(documento, nome, acao);

    public ElementosFiltrados FiltrarClassificaveis(IReadOnlyCollection<long> ids)
    {
        var aceitos = new List<long>();
        var recusados = new List<PontoIgnorado>();
        foreach (var id in ids)
        {
            var elemento = documento.GetElement(new ElementId(id));
            var motivo = elemento switch
            {
                null => "elemento não existe no documento",
                _ when ParametrosAmpere.Ler(elemento, ParametrosAmpere.TipoCarga) is null =>
                    $"categoria '{elemento.Category?.Name}' não recebe AMP_TipoCarga (selecione luminárias, tomadas ou equipamentos; rode 'Injetar parâmetros' se ainda não rodou)",
                _ when ParametrosAmpere.Ler(elemento, ParametrosAmpere.TipoCarga)!.IsReadOnly => "parâmetros somente leitura (elemento em grupo ou vínculo?)",
                _ => null
            };

            if (motivo is null) aceitos.Add(id);
            else recusados.Add(new PontoIgnorado(id, motivo));
        }

        return new ElementosFiltrados(aceitos, recusados);
    }

    public void Classificar(IReadOnlyCollection<long> ids, ClassificacaoDeCarga classificacao)
    {
        var codigo = CodigosDeTipoDeCarga.Codigo(classificacao.Tipo);
        foreach (var id in ids)
        {
            var elemento = documento.GetElement(new ElementId(id));
            ParametrosAmpere.GravarTexto(elemento, ParametrosAmpere.TipoCarga, codigo);
            if (classificacao.PotenciaVA is { } potencia)
            {
                ParametrosAmpere.GravarNumero(elemento, ParametrosAmpere.PotenciaInstaladaVA,
                    UnitUtils.ConvertToInternalUnits((double)potencia, UnitTypeId.VoltAmperes));
            }

            if (classificacao.FatorDePotencia is { } fator) ParametrosAmpere.GravarNumero(elemento, ParametrosAmpere.FatorPotencia, (double)fator);
            if (classificacao.TensaoV is { } tensao) ParametrosAmpere.GravarNumero(elemento, ParametrosAmpere.TensaoCircuitoV, (double)tensao);
            if (classificacao.Fases is { } fases) ParametrosAmpere.GravarTexto(elemento, ParametrosAmpere.Fases, fases);
            // O local é do ponto: num circuito selecionado junto (AMP_Local não vai em circuitos), fica de fora.
            if (classificacao.Local is { } local && ParametrosAmpere.Ler(elemento, ParametrosAmpere.Local) is not null)
                ParametrosAmpere.GravarTexto(elemento, ParametrosAmpere.Local, local);
            // Aparelho só em TUE; nos outros tipos, o anterior é apagado (o Core já recusou aparelho fora de TUE).
            if (ParametrosAmpere.Ler(elemento, ParametrosAmpere.Aparelho) is not null)
            {
                if (classificacao.Tipo != TipoDeCarga.TUE) ParametrosAmpere.GravarTextoOuApagar(elemento, ParametrosAmpere.Aparelho, null);
                else if (classificacao.Aparelho is { } aparelho) ParametrosAmpere.GravarTexto(elemento, ParametrosAmpere.Aparelho, CodigosDeAparelho.Codigo(aparelho));
            }
        }
    }

    public LeituraDePontos LerPontos(IReadOnlyCollection<long> ids)
    {
        var pontos = new List<PontoDeCarga>();
        var recusados = new List<PontoIgnorado>();
        foreach (var id in ids)
        {
            if (documento.GetElement(new ElementId(id)) is not FamilyInstance instancia || ConectorDeForca(instancia) is not { } conector)
            {
                recusados.Add(new PontoIgnorado(id, "não é carga elétrica (sem conector elétrico de força)"));
                continue;
            }

            var tipo = CodigosDeTipoDeCarga.TryLer(ParametrosAmpere.LerTexto(instancia, ParametrosAmpere.TipoCarga), out var lido) ? lido : (TipoDeCarga?)null;
            pontos.Add(new PontoDeCarga(id, tipo, LerPotenciaVA(instancia), Alimentacao(conector), CircuitoAtual(instancia),
                ParametrosAmpere.Ler(instancia, ParametrosAmpere.TensaoCircuitoV) is { HasValue: true } tensao ? (decimal)tensao.AsDouble() : null,
                ParametrosAmpere.LerTexto(instancia, ParametrosAmpere.Fases) is { Length: > 0 } fases ? fases : null));
        }

        return new LeituraDePontos(pontos, recusados);
    }

    public QuadroEletrico LerQuadro(long quadroId)
    {
        var painel = Painel(quadroId);
        var numeros = painel.MEPModel.GetAssignedElectricalSystems()
            .Select(sistema => ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito))
            .OfType<string>()
            .Where(numero => numero.Length > 0)
            .ToList();
        return new QuadroEletrico(quadroId, NomeDoPainel(painel), numeros);
    }

    public void CriarCircuitos(long quadroId, IReadOnlyList<CircuitoPlanejado> circuitos)
    {
        var painel = Painel(quadroId);
        var nomeDoQuadro = NomeDoPainel(painel);
        foreach (var circuito in circuitos)
        {
            var membros = circuito.Pontos.Select(id => new ElementId(id)).ToList();
            ElectricalSystem sistema;
            try
            {
                sistema = ElectricalSystem.Create(documento, membros, ElectricalSystemType.PowerCircuit)
                          ?? throw new InvalidOperationException("o Revit não criou o circuito");
                sistema.SelectPanel(painel);
            }
            catch (Exception excecao) when (excecao is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException)
            {
                throw new InvalidOperationException($"Circuito {circuito.Numero} no quadro {nomeDoQuadro}: {excecao.Message}", excecao);
            }

            ParametrosAmpere.GravarTexto(sistema, ParametrosAmpere.NumeroCircuito, circuito.Numero);
            ParametrosAmpere.GravarTexto(sistema, ParametrosAmpere.TipoCarga, CodigosDeTipoDeCarga.Codigo(circuito.Tipo));
            ParametrosAmpere.GravarTexto(sistema, ParametrosAmpere.Quadro, nomeDoQuadro);
            foreach (var membro in membros.Select(documento.GetElement))
            {
                ParametrosAmpere.GravarTexto(membro, ParametrosAmpere.NumeroCircuito, circuito.Numero);
                ParametrosAmpere.GravarTexto(membro, ParametrosAmpere.Quadro, nomeDoQuadro);
            }
        }
    }

    /// <summary>Os parâmetros Ampere usados pela classificação e pelos circuitos já estão no documento?</summary>
    /// <remarks>AMP_Local entrou no catálogo 0.2 e AMP_Aparelho no 0.4: projeto injetado antes precisa de nova injeção.</remarks>
    public bool ParametrosInjetados() =>
        new[] { ParametrosAmpere.TipoCarga, ParametrosAmpere.PotenciaInstaladaVA, ParametrosAmpere.NumeroCircuito, ParametrosAmpere.Quadro, ParametrosAmpere.Local, ParametrosAmpere.Aparelho }
            .All(definicao => ParametrosAmpere.Injetado(documento, definicao));

    /// <summary>Quadros do documento (equipamento elétrico com conector de força), para o projetista escolher.</summary>
    public IReadOnlyList<QuadroEletrico> ListarQuadros() =>
        new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(instancia => ConectorDeForca(instancia) is not null)
            .Select(instancia => LerQuadro(instancia.Id.Value))
            .OrderBy(quadro => quadro.Nome, StringComparer.CurrentCulture)
            .ToList();

    private FamilyInstance Painel(long quadroId) =>
        documento.GetElement(new ElementId(quadroId)) as FamilyInstance
        ?? throw new InvalidOperationException($"O quadro {quadroId} não existe no documento.");

    private static string NomeDoPainel(FamilyInstance painel) =>
        painel.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME)?.AsString() is { Length: > 0 } nome ? nome : painel.Name;

    private static Connector? ConectorDeForca(FamilyInstance instancia) => Revit.ConectorDeForca.De(instancia);

    private static string Alimentacao(Connector conector)
    {
        var info = conector.GetMEPConnectorInfo() as MEPFamilyConnectorInfo;
        var tensao = info?.GetConnectorParameterValue(new ElementId(BuiltInParameter.RBS_ELEC_VOLTAGE)) as DoubleParameterValue;
        var polos = info?.GetConnectorParameterValue(new ElementId(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES)) as IntegerParameterValue;
        if (tensao is null || polos is null) return "alimentação desconhecida";

        var volts = UnitUtils.ConvertFromInternalUnits(tensao.Value, UnitTypeId.Volts);
        return string.Create(CultureInfo.InvariantCulture, $"{volts:0.##} V · {polos.Value} polo{(polos.Value == 1 ? string.Empty : "s")}");
    }

    private static decimal? LerPotenciaVA(Element elemento) =>
        ParametrosAmpere.Ler(elemento, ParametrosAmpere.PotenciaInstaladaVA) is { HasValue: true } parametro
            ? (decimal)UnitUtils.ConvertFromInternalUnits(parametro.AsDouble(), UnitTypeId.VoltAmperes)
            : null;

    private static string? CircuitoAtual(FamilyInstance instancia)
    {
        foreach (var sistema in instancia.MEPModel.GetElectricalSystems())
        {
            if (sistema.SystemType != ElectricalSystemType.PowerCircuit) continue;
            return ParametrosAmpere.LerTexto(sistema, ParametrosAmpere.NumeroCircuito) is { Length: > 0 } numero ? numero : sistema.Name;
        }

        return null;
    }
}
