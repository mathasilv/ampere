using System.Text;
using Ampere.Core.Memoria;
using Ampere.Core.Relatorios;
using PdfSharp.Pdf.IO;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>PDF real, com o PDFsharp e as fontes embutidas; o conteúdo página a página é conferido nos golden files.</summary>
public class PdfDoRelatorio_Teste
{
    [Test]
    public async Task Pdf_valido_reabre_com_titulo_identificador_e_varias_paginas()
    {
        var memoria = Memoria("TUG-01", passos: 40);

        var pdf = RelatorioDeMemoria.Pdf(memoria);

        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
        using var documento = PdfReader.Open(new MemoryStream(pdf));
        await Assert.That(documento.PageCount).IsGreaterThan(1);
        await Assert.That(documento.Info.Title).IsEqualTo("Memória de cálculo — circuito TUG-01");
        await Assert.That(documento.Info.Subject).IsEqualTo(memoria.Hash());
        await Assert.That(documento.Info.Creator).IsEqualTo("Ampere");
    }

    [Test]
    public async Task Fontes_DejaVu_embutidas_como_subconjunto()
    {
        var conteudo = Encoding.Latin1.GetString(RelatorioDeMemoria.Pdf(Memoria("TUG-01")));

        await Assert.That(conteudo).Contains("+DejaVu#20Sans/");
        await Assert.That(conteudo).Contains("+DejaVu#20Sans,Bold");
        await Assert.That(conteudo).Contains("+DejaVu#20Sans#20Mono");
        await Assert.That(conteudo).Contains("/FontFile2");
    }

    private static MemoriaDeCalculo Memoria(string circuito, int passos = 3) =>
        new(circuito, "NBR5410:2004", Enumerable.Range(1, passos)
            .Select(numero => new PassoDeCalculo("Tabela 36", $"Passo {numero}", "ΔV% = k · ρ · L · IB / (S · V) · 100 ≤ ΔV%máx",
                [new ValorDoPasso("ρ", 0.01724m, "Ω·mm²/m"), new ValorDoPasso("θ", 30m, "°C")], numero, "A",
                "texto com √3, IZ₀ e In ∈ correntes nominais"))
            .ToList());
}
