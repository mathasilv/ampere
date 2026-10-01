using System.Globalization;
using System.Text;

namespace Ampere.Core.Diagramas;

/// <summary>Circuito como aparece no unifilar: identificação, carga e o que o 'Dimensionar circuitos' gravou (nulo = não calculado).</summary>
/// <param name="Descricao">Nome da carga do circuito (descrição livre do projetista).</param>
/// <param name="Fases">Configuração (F+N, 2F, 3F, ...).</param>
public sealed record CircuitoDoUnifilar(
    string Numero,
    string? Descricao,
    string? Tipo,
    string? Fases,
    decimal? TensaoV,
    decimal? PotenciaVA,
    decimal? DisjuntorA,
    decimal? SecaoMm2,
    decimal? IdrNominalA,
    decimal? IdrSensibilidadeMa,
    decimal? QuedaPct);

/// <summary>Quadro com os circuitos, na ordem em que aparecem no diagrama.</summary>
public sealed record QuadroDoUnifilar(string Nome, IReadOnlyList<CircuitoDoUnifilar> Circuitos);

/// <summary>Alinhamento horizontal de um texto em relação ao ponto de inserção.</summary>
public enum AlinhamentoDoTexto
{
    Esquerda,
    Centro,
    Direita
}

/// <summary>Primitiva do desenho, em milímetros de papel, com o eixo Y para cima (como no Revit).</summary>
public abstract record ElementoDoDesenho;

/// <summary>Segmento de reta; <paramref name="Grosso" /> para o barramento.</summary>
public sealed record Segmento(decimal X1, decimal Y1, decimal X2, decimal Y2, bool Grosso = false) : ElementoDoDesenho;

/// <summary>Texto com a base em (X, Y).</summary>
public sealed record Texto(decimal X, decimal Y, string Conteudo, AlinhamentoDoTexto Alinhamento, decimal Altura) : ElementoDoDesenho;

/// <summary>Desenho pronto para o Revit (vista de desenho) ou para SVG: largura e altura do papel, em mm.</summary>
public sealed record DesenhoDoUnifilar(string Titulo, decimal Largura, decimal Altura, IReadOnlyList<ElementoDoDesenho> Elementos);

/// <summary>Porta para os quadros e o desenho dos diagramas num documento (implementada pelo adapter Revit).</summary>
public interface IDocumentoDeDiagramas : IDocumentoTransacional
{
    /// <summary>Quadros com circuitos, cada um com os circuitos na ordem do diagrama.</summary>
    IReadOnlyList<QuadroDoUnifilar> LerQuadros();

    /// <summary>Desenha (ou redesenha, na mesma vista) o unifilar do quadro; devolve o nome da vista.</summary>
    string DesenharUnifilar(string nomeDoQuadro, DesenhoDoUnifilar desenho);
}

/// <summary>Unifilar de um quadro: a vista em que foi desenhado e o desenho (para o SVG).</summary>
public sealed record UnifilarDesenhado(string Quadro, string Vista, DesenhoDoUnifilar Desenho);

/// <summary>Caso de uso "Diagramas unifilares": um diagrama por quadro com circuitos, todos numa única transação.</summary>
public static class DiagramasDoProjeto
{
    /// <summary>Nome da transação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: diagramas unifilares";

    public static IReadOnlyList<UnifilarDesenhado> Desenhar(IDocumentoDeDiagramas documento)
    {
        var quadros = documento.LerQuadros();
        var desenhados = new List<UnifilarDesenhado>();
        if (quadros.Count == 0) return desenhados;

        documento.EmUmaTransacao(NomeDaTransacao, () =>
        {
            foreach (var quadro in quadros)
            {
                var desenho = DiagramaUnifilar.Montar(quadro);
                desenhados.Add(new UnifilarDesenhado(quadro.Nome, documento.DesenharUnifilar(quadro.Nome, desenho), desenho));
            }
        });
        return desenhados;
    }
}

/// <summary>
///     Diagrama unifilar de um quadro: alimentação, barramento e uma derivação por circuito com disjuntor, IDR (se houver),
///     seção do condutor, queda de tensão e a identificação da carga.
/// </summary>
/// <remarks>
///     Representação esquemática, sem simbologia normativa: dispositivos são caixas com o valor por cima. Os valores são os
///     gravados pelo 'Dimensionar circuitos' — circuito sem disjuntor aparece como "não dimensionado", nunca com valor
///     presumido. Geometria determinística, em decimal: o mesmo quadro dá sempre o mesmo desenho.
/// </remarks>
public static class DiagramaUnifilar
{
    private const decimal Largura = 200m;
    private const decimal XBarramento = 20m;
    private const decimal PrimeiraDerivacao = -28m;
    private const decimal Espacamento = 12m;
    private const decimal AlturaDoTitulo = 3.5m;
    private const decimal AlturaNormal = 2.5m;
    private const decimal AlturaPequena = 2m;

    public static DesenhoDoUnifilar Montar(QuadroDoUnifilar quadro)
    {
        var elementos = new List<ElementoDoDesenho>();
        var titulo = $"{quadro.Nome} — diagrama unifilar";
        elementos.Add(new Texto(0m, 0m, titulo, AlinhamentoDoTexto.Esquerda, AlturaDoTitulo));
        elementos.Add(new Texto(0m, -6m, Resumo(quadro), AlinhamentoDoTexto.Esquerda, AlturaNormal));

        // Alimentação e barramento.
        var fimDoBarramento = quadro.Circuitos.Count == 0 ? -22m : PrimeiraDerivacao - (quadro.Circuitos.Count - 1) * Espacamento - 3m;
        elementos.Add(new Segmento(XBarramento, -12m, XBarramento, -20m));
        elementos.Add(new Texto(XBarramento + 2m, -16m, "alimentação", AlinhamentoDoTexto.Esquerda, AlturaPequena));
        elementos.Add(new Segmento(XBarramento, -20m, XBarramento, fimDoBarramento, Grosso: true));

        for (var indice = 0; indice < quadro.Circuitos.Count; indice++)
            Derivacao(elementos, quadro.Circuitos[indice], PrimeiraDerivacao - indice * Espacamento);

        var rodape = fimDoBarramento - 8m;
        if (quadro.Circuitos.Count == 0) elementos.Add(new Texto(XBarramento + 4m, -24m, "quadro sem circuitos", AlinhamentoDoTexto.Esquerda, AlturaNormal));
        elementos.Add(new Texto(0m, rodape, "Representação esquemática, sem simbologia normativa. Valores do 'Dimensionar circuitos'; a memória de cada",
            AlinhamentoDoTexto.Esquerda, AlturaPequena));
        elementos.Add(new Texto(0m, rodape - 3.5m, "circuito está no hash AMP_MemoriaCalculoId. \"não dimensionado\": rode o 'Dimensionar circuitos' ou veja o motivo no resumo.",
            AlinhamentoDoTexto.Esquerda, AlturaPequena));

        return new DesenhoDoUnifilar(titulo, Largura, -(rodape - 3.5m) + 5m, elementos);
    }

    private static void Derivacao(List<ElementoDoDesenho> elementos, CircuitoDoUnifilar circuito, decimal y)
    {
        // Barramento → disjuntor (caixa 30–38) → IDR (caixa 44–52, se houver) → condutor (até 90) → identificação.
        elementos.Add(new Segmento(XBarramento, y, 30m, y));
        Caixa(elementos, 30m, y);
        elementos.Add(new Texto(34m, y + 3m, circuito.DisjuntorA is { } disjuntor ? $"{Numero(disjuntor)} A" : "—", AlinhamentoDoTexto.Centro, AlturaPequena));

        decimal inicioDoCondutor;
        if (circuito.IdrSensibilidadeMa is { } sensibilidade)
        {
            elementos.Add(new Segmento(38m, y, 44m, y));
            Caixa(elementos, 44m, y);
            var nominal = circuito.IdrNominalA is { } idr ? $"IDR {Numero(idr)} A" : "IDR";
            elementos.Add(new Texto(48m, y + 3m, nominal, AlinhamentoDoTexto.Centro, AlturaPequena));
            elementos.Add(new Texto(48m, y - 4.5m, $"{Numero(sensibilidade)} mA", AlinhamentoDoTexto.Centro, AlturaPequena));
            inicioDoCondutor = 52m;
        }
        else
        {
            inicioDoCondutor = 38m;
        }

        elementos.Add(new Segmento(inicioDoCondutor, y, 90m, y));
        var condutor = circuito.SecaoMm2 is { } secao ? $"{Numero(secao)} mm²" : "não dimensionado";
        elementos.Add(new Texto(74m, y + 1m, condutor, AlinhamentoDoTexto.Centro, AlturaPequena));
        // No desenho, duas casas bastam; o valor completo está na memória do circuito.
        if (circuito.QuedaPct is { } queda)
            elementos.Add(new Texto(74m, y - 3m, $"ΔV {Numero(Math.Round(queda, 2, MidpointRounding.AwayFromZero))}%", AlinhamentoDoTexto.Centro, AlturaPequena));
        elementos.Add(new Texto(92m, y - 0.8m, Identificacao(circuito), AlinhamentoDoTexto.Esquerda, AlturaNormal));
    }

    private static void Caixa(List<ElementoDoDesenho> elementos, decimal x, decimal y)
    {
        elementos.Add(new Segmento(x, y - 2m, x + 8m, y - 2m));
        elementos.Add(new Segmento(x + 8m, y - 2m, x + 8m, y + 2m));
        elementos.Add(new Segmento(x + 8m, y + 2m, x, y + 2m));
        elementos.Add(new Segmento(x, y + 2m, x, y - 2m));
    }

    private static string Identificacao(CircuitoDoUnifilar circuito)
    {
        var partes = new List<string> { circuito.Numero };
        if (!string.IsNullOrWhiteSpace(circuito.Descricao)) partes.Add(circuito.Descricao.Trim());
        if (!string.IsNullOrWhiteSpace(circuito.Tipo)) partes.Add(circuito.Tipo.Trim());
        var alimentacao = string.Join(" ", new[] { circuito.Fases, circuito.TensaoV is { } tensao ? $"{Numero(tensao)} V" : null }.OfType<string>());
        if (alimentacao.Length > 0) partes.Add(alimentacao);
        if (circuito.PotenciaVA is { } potencia) partes.Add($"{Numero(potencia)} VA");
        return string.Join(" — ", partes);
    }

    private static string Resumo(QuadroDoUnifilar quadro)
    {
        var circuitos = $"{quadro.Circuitos.Count} {(quadro.Circuitos.Count == 1 ? "circuito" : "circuitos")}";
        if (quadro.Circuitos.Count == 0) return circuitos;

        var potencias = quadro.Circuitos.Select(circuito => circuito.PotenciaVA).ToList();
        var instalada = potencias.All(potencia => potencia is not null)
            ? $"potência instalada {Numero(potencias.Sum(potencia => potencia!.Value))} VA"
            : "potência instalada incompleta (circuito sem potência)";
        return $"{circuitos} · {instalada}";
    }

    private static string Numero(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);

    /// <summary>O desenho em SVG (prévia e relatório), em milímetros, com o Y invertido para a tela.</summary>
    public static string Svg(DesenhoDoUnifilar desenho)
    {
        var texto = new StringBuilder();
        var margem = 5m;
        var largura = desenho.Largura + 2 * margem;
        var altura = desenho.Altura + 2 * margem;
        texto.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(largura)}mm\" height=\"{N(altura)}mm\" viewBox=\"{N(-margem)} {N(-margem - AlturaDoTitulo)} {N(largura)} {N(altura)}\">\n");
        texto.Append($"<title>{Xml(desenho.Titulo)}</title>\n");
        texto.Append("<rect x=\"-1000\" y=\"-1000\" width=\"3000\" height=\"3000\" fill=\"#ffffff\"/>\n");
        texto.Append("<g stroke=\"#000000\" fill=\"none\" stroke-linecap=\"square\">\n");
        foreach (var segmento in desenho.Elementos.OfType<Segmento>())
        {
            texto.Append($"<line x1=\"{N(segmento.X1)}\" y1=\"{N(-segmento.Y1)}\" x2=\"{N(segmento.X2)}\" y2=\"{N(-segmento.Y2)}\" stroke-width=\"{(segmento.Grosso ? "0.7" : "0.25")}\"/>\n");
        }

        texto.Append("</g>\n<g font-family=\"DejaVu Sans, Arial, sans-serif\" fill=\"#000000\">\n");
        foreach (var item in desenho.Elementos.OfType<Texto>())
        {
            var ancora = item.Alinhamento switch
            {
                AlinhamentoDoTexto.Centro => "middle",
                AlinhamentoDoTexto.Direita => "end",
                _ => "start"
            };
            texto.Append($"<text x=\"{N(item.X)}\" y=\"{N(-item.Y)}\" font-size=\"{N(item.Altura)}\" text-anchor=\"{ancora}\">{Xml(item.Conteudo)}</text>\n");
        }

        texto.Append("</g>\n</svg>\n");
        return texto.ToString();
    }

    private static string N(decimal valor) => (valor == 0m ? 0m : valor).ToString("0.###", CultureInfo.InvariantCulture);

    private static string Xml(string valor) =>
        valor.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
