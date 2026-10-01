using Ampere.Core;
using Ampere.Core.Diagramas;
using Ampere.Core.Parametros;
using Ampere.Revit.Quadros;
using Autodesk.Revit.DB.Electrical;

namespace Ampere.Revit.Diagramas;

/// <summary>
///     Porta <see cref="IDocumentoDeDiagramas" /> sobre a API do Revit: lê os circuitos dos quadros e desenha o unifilar
///     numa vista de desenho (Drafting View) com linhas e textos de detalhe.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Os valores vêm dos parâmetros que o 'Dimensionar circuitos' grava; 0 é o "apagado" do Ampere (parâmetro
///         numérico não volta a vazio) e aparece como não calculado.</item>
///         <item>A vista é reaproveitada: só os elementos de detalhe dela são apagados e redesenhados, para não sair das
///         pranchas. Escala 1:1, para o desenho em mm de papel ficar com o tamanho certo na prancha.</item>
///         <item>Os textos usam o tipo de texto padrão do projeto (a altura é a dele, não a do SVG); o barramento é
///         desenhado com três linhas paralelas, sem depender de um estilo de linha do template.</item>
///     </list>
/// </remarks>
public sealed class DocumentoDeDiagramasRevit(Document documento) : IDocumentoDeDiagramas
{
    private static readonly double Milimetro = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);

    public void EmUmaTransacao(string nome, Action acao) => TransacaoRevit.Executar(documento, nome, acao);

    public IReadOnlyList<QuadroDoUnifilar> LerQuadros()
    {
        var rotulos = LeituraDoPainel.RotulosDasFases(documento);
        return new FilteredElementCollector(documento)
            .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Select(painel => (Painel: painel, Sistemas: SistemasDeForca(painel)))
            .Where(par => par.Sistemas.Count > 0)
            .Select(par => new QuadroDoUnifilar(
                LeituraDoPainel.Nome(par.Painel),
                par.Sistemas.Select(sistema => LerCircuito(sistema) with { FasesNoQuadro = LeituraDoPainel.FasesNoQuadro(sistema, rotulos) })
                    .OrderBy(circuito => circuito.Numero, StringComparer.Ordinal).ToList(),
                Alimentacao(par.Painel, rotulos),
                Alimentador(par.Painel)))
            .OrderBy(quadro => quadro.Nome, StringComparer.CurrentCulture)
            .ToList();
    }

    // Esquema e tensões do quadro, como no quadro de cargas (ex.: "3F+N 220/127 V").
    private string? Alimentacao(FamilyInstance painel, string[] rotulos) =>
        LeituraDoPainel.Alimentacao(documento, painel, rotulos) is { } alimentacao
            ? alimentacao.FaseNeutro is { } faseNeutro && alimentacao.Esquema != "F+N"
                ? $"{alimentacao.Esquema} {Formatar(alimentacao.TensaoV)}/{Formatar(faseNeutro)} V"
                : $"{alimentacao.Esquema} {Formatar(alimentacao.TensaoV)} V"
            : null;

    // O circuito de que o quadro é carga, com o que o 'Dimensionar alimentadores' gravou (0 = não calculado).
    private static AlimentadorDoUnifilar? Alimentador(FamilyInstance painel)
    {
        var alimentador = painel.MEPModel?.GetElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .MinBy(sistema => sistema.Id.Value);
        if (alimentador is null) return null;

        return new AlimentadorDoUnifilar(
            alimentador.BaseEquipment is { } origem ? LeituraDoPainel.Nome(origem) : null,
            Numero(alimentador, ParametrosAmpere.DisjuntorNominalA),
            Numero(alimentador, ParametrosAmpere.BitolaCondutorMm2),
            Numero(alimentador, ParametrosAmpere.IdrNominalA),
            Numero(alimentador, ParametrosAmpere.IdrSensibilidadeMa),
            Numero(alimentador, ParametrosAmpere.QuedaTensaoPct));
    }

    private static string Formatar(decimal volts) => NumeroEmTexto.FormatarParaLeitura(Math.Round(volts, 0, MidpointRounding.AwayFromZero));

    public string DesenharUnifilar(string nomeDoQuadro, DesenhoDoUnifilar desenho)
    {
        var nome = $"{nomeDoQuadro} — unifilar (Ampere)";
        var vista = new FilteredElementCollector(documento)
                        .OfCategory(BuiltInCategory.OST_Views)
                        .OfClass(typeof(ViewDrafting))
                        .Cast<ViewDrafting>()
                        .FirstOrDefault(view => view.Name == nome)
                    ?? NovaVista(nome);

        var antigos = new FilteredElementCollector(documento, vista.Id)
            .WherePasses(new ElementMulticategoryFilter([BuiltInCategory.OST_Lines, BuiltInCategory.OST_TextNotes]))
            .ToElementIds();
        if (antigos.Count > 0) documento.Delete(antigos);

        foreach (var segmento in desenho.Elementos.OfType<Segmento>())
        {
            var deslocamentos = segmento.Grosso ? new[] { -0.25m, 0m, 0.25m } : [0m];
            foreach (var deslocamento in deslocamentos)
            {
                documento.Create.NewDetailCurve(vista, Line.CreateBound(
                    Ponto(segmento.X1 + deslocamento, segmento.Y1), Ponto(segmento.X2 + deslocamento, segmento.Y2)));
            }
        }

        var tipoDeTexto = documento.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        foreach (var texto in desenho.Elementos.OfType<Texto>())
        {
            var opcoes = new TextNoteOptions(tipoDeTexto)
            {
                HorizontalAlignment = texto.Alinhamento switch
                {
                    AlinhamentoDoTexto.Centro => HorizontalTextAlignment.Center,
                    AlinhamentoDoTexto.Direita => HorizontalTextAlignment.Right,
                    _ => HorizontalTextAlignment.Left
                }
            };
            // A origem da nota de texto é o topo da caixa; no desenho, (X, Y) é a base da linha.
            TextNote.Create(documento, vista.Id, Ponto(texto.X, texto.Y + texto.Altura), texto.Conteudo, opcoes);
        }

        return nome;
    }

    private ViewDrafting NovaVista(string nome)
    {
        var tipo = new FilteredElementCollector(documento)
                       .OfClass(typeof(ViewFamilyType))
                       .Cast<ViewFamilyType>()
                       .FirstOrDefault(tipo => tipo.ViewFamily == ViewFamily.Drafting)
                   ?? throw new InvalidOperationException("O projeto não tem tipo de vista de desenho (Drafting View).");
        var vista = ViewDrafting.Create(documento, tipo.Id);
        vista.Name = nome;
        vista.Scale = 1;
        return vista;
    }

    private static XYZ Ponto(decimal x, decimal y) => new((double)x * Milimetro, (double)y * Milimetro, 0);

    private static List<ElectricalSystem> SistemasDeForca(FamilyInstance painel) =>
        painel.MEPModel.GetAssignedElectricalSystems()?
            .Where(sistema => sistema.SystemType == ElectricalSystemType.PowerCircuit)
            .ToList() ?? [];

    private static CircuitoDoUnifilar LerCircuito(ElectricalSystem sistema)
    {
        var membros = sistema.Elements.Cast<Element>().ToList();
        return new CircuitoDoUnifilar(
            Texto(sistema, ParametrosAmpere.NumeroCircuito) ?? sistema.Name,
            sistema.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NAME)?.AsString() is { Length: > 0 } descricao ? descricao : null,
            Texto(sistema, ParametrosAmpere.TipoCarga),
            membros.Select(membro => Texto(membro, ParametrosAmpere.Fases)).FirstOrDefault(fases => fases is not null),
            membros.Select(membro => Numero(membro, ParametrosAmpere.TensaoCircuitoV)).FirstOrDefault(tensao => tensao is not null),
            Potencia(membros),
            Numero(sistema, ParametrosAmpere.DisjuntorNominalA),
            Numero(sistema, ParametrosAmpere.BitolaCondutorMm2),
            Numero(sistema, ParametrosAmpere.IdrNominalA),
            Numero(sistema, ParametrosAmpere.IdrSensibilidadeMa),
            Numero(sistema, ParametrosAmpere.QuedaTensaoPct));
    }

    private static decimal? Potencia(IReadOnlyList<Element> membros)
    {
        decimal? total = null;
        foreach (var membro in membros)
        {
            if (ParametrosAmpere.Ler(membro, ParametrosAmpere.PotenciaInstaladaVA) is not { HasValue: true } parametro) continue;
            total = (total ?? 0m) + (decimal)UnitUtils.ConvertFromInternalUnits(parametro.AsDouble(), UnitTypeId.VoltAmperes);
        }

        return total;
    }

    private static string? Texto(Element elemento, DefinicaoDeParametro definicao) =>
        ParametrosAmpere.LerTexto(elemento, definicao) is { Length: > 0 } texto ? texto : null;

    // 0 é o valor "apagado" do Ampere: no diagrama, não calculado.
    private static decimal? Numero(Element elemento, DefinicaoDeParametro definicao) =>
        ParametrosAmpere.Ler(elemento, definicao) is { HasValue: true } parametro && parametro.AsDouble() is var valor and not 0d ? (decimal)valor : null;

}
