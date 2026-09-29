using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Ampere.Core.Relatorios.Pdf;

/// <summary>
///     Desenha o conteúdo do relatório num PDF A4 com o PDFsharp. O conteúdo é determinístico (fontes embutidas e
///     diagramação própria, conferidos pelo <see cref="Roteiro" /> nos golden files); os metadados são os de qualquer
///     PDF — data de criação e identificadores sorteados pelo PDFsharp. O vínculo com a memória fica no Assunto.
/// </summary>
internal static class PdfDoRelatorio
{
    private static readonly XPdfFontOptions Unicode = new(PdfFontEncoding.Unicode);

    public static byte[] Gerar(ConteudoDoRelatorio conteudo, string identificador)
    {
        FontesDoRelatorio.Registrar();
        var fontes = Fontes();
        using var documento = new PdfDocument();
        documento.Info.Title = conteudo.Titulo;
        documento.Info.Subject = identificador;
        documento.Info.Creator = "Ampere";
        documento.Language = "pt-BR";

        IReadOnlyList<PaginaDiagramada> paginas;
        using (var medida = XGraphics.CreateMeasureContext(new XSize(Folha.A4.Largura, Folha.A4.Altura), XGraphicsUnit.Point,
                   XPageDirection.Downwards, documento.RenderEvents))
        {
            paginas = new DiagramacaoDoRelatorio(new Medidor(medida, fontes), Folha.A4).Diagramar(conteudo);
        }

        foreach (var pagina in paginas)
        {
            var folha = documento.AddPage();
            folha.Size = PageSize.A4;
            using var grafico = XGraphics.FromPdfPage(folha);
            foreach (var linha in pagina.Linhas.Append(pagina.Rodape))
            {
                foreach (var trecho in linha.Trechos)
                    grafico.DrawString(trecho.Texto, fontes[trecho.Estilo], Pincel(trecho.Estilo), trecho.X, linha.Y, XStringFormats.TopLeft);
            }
        }

        using var saida = new MemoryStream();
        documento.Save(saida, false);
        return saida.ToArray();
    }

    /// <summary>
    ///     O texto de cada página com posição e estilo — o "golden file" do PDF: legível, e igual em qualquer máquina
    ///     porque a métrica vem das fontes embutidas (os bytes do PDF variam com a compressão de cada runtime).
    /// </summary>
    public static string Roteiro(ConteudoDoRelatorio conteudo)
    {
        FontesDoRelatorio.Registrar();
        var fontes = Fontes();
        using var documento = new PdfDocument();
        using var medida = XGraphics.CreateMeasureContext(new XSize(Folha.A4.Largura, Folha.A4.Altura), XGraphicsUnit.Point,
            XPageDirection.Downwards, documento.RenderEvents);
        var paginas = new DiagramacaoDoRelatorio(new Medidor(medida, fontes), Folha.A4).Diagramar(conteudo);

        var texto = new StringBuilder();
        foreach (var (pagina, indice) in paginas.Select((pagina, indice) => (pagina, indice)))
        {
            texto.Append($"== página {indice + 1} de {paginas.Count}\n");
            foreach (var linha in pagina.Linhas.Append(pagina.Rodape))
            {
                texto.Append(Numero(linha.Y));
                foreach (var trecho in linha.Trechos) texto.Append($" [{trecho.Estilo} {Numero(trecho.X)}] {trecho.Texto}");
                texto.Append('\n');
            }
        }

        return texto.ToString();
    }

    private static Dictionary<EstiloDeTexto, XFont> Fontes() => new()
    {
        [EstiloDeTexto.Titulo] = new XFont(FontesDoRelatorio.Sans, 15, XFontStyleEx.Bold, Unicode),
        [EstiloDeTexto.Secao] = new XFont(FontesDoRelatorio.Sans, 12, XFontStyleEx.Bold, Unicode),
        [EstiloDeTexto.TituloDoPasso] = new XFont(FontesDoRelatorio.Sans, 10.5, XFontStyleEx.Bold, Unicode),
        [EstiloDeTexto.Rotulo] = new XFont(FontesDoRelatorio.Sans, 9.5, XFontStyleEx.Bold, Unicode),
        [EstiloDeTexto.Texto] = new XFont(FontesDoRelatorio.Sans, 9.5, XFontStyleEx.Regular, Unicode),
        [EstiloDeTexto.Codigo] = new XFont(FontesDoRelatorio.Mono, 9, XFontStyleEx.Regular, Unicode),
        [EstiloDeTexto.Nota] = new XFont(FontesDoRelatorio.Sans, 8.5, XFontStyleEx.Regular, Unicode),
        [EstiloDeTexto.Rodape] = new XFont(FontesDoRelatorio.Sans, 8, XFontStyleEx.Regular, Unicode)
    };

    private static XBrush Pincel(EstiloDeTexto estilo) => estilo is EstiloDeTexto.Nota or EstiloDeTexto.Rodape ? XBrushes.DimGray : XBrushes.Black;

    private static string Numero(double valor) => NumeroEmTexto.Formatar(Math.Round((decimal)valor, 1, MidpointRounding.AwayFromZero));

    private sealed class Medidor(XGraphics grafico, IReadOnlyDictionary<EstiloDeTexto, XFont> fontes) : IMedidorDeTexto
    {
        public double Largura(string texto, EstiloDeTexto estilo) => grafico.MeasureString(texto, fontes[estilo]).Width;

        public double AlturaDaLinha(EstiloDeTexto estilo) => fontes[estilo].GetHeight();
    }
}
