using System.Globalization;
using Ampere.Core.Relatorios;
using Ampere.Core.Relatorios.Pdf;

namespace Ampere.Tests.Core.Relatorios;

/// <summary>
///     Diagramação com medidor falso: cada caractere mede 1 e cada linha 10, numa folha 100 x 200 com margem 10 e
///     rodapé 10 — largura útil 80, conteúdo até y = 180.
/// </summary>
public class DiagramacaoDoRelatorio_Teste
{
    private static readonly Folha Pequena = new(100, 200, 10, 10);

    [Test]
    public async Task Rotulo_e_texto_na_mesma_linha_com_o_recuo_dos_campos()
    {
        var paginas = Diagramar(Conteudo(Passo("1. Passo", new Campo("Resultado", "10 A"))));

        var linha = Linhas(paginas).Single(linha => linha.Trechos[0].Texto == "Resultado:");
        await Assert.That(linha.Trechos).IsEquivalentTo(
            [new Trecho(22, "Resultado:", EstiloDeTexto.Rotulo), new Trecho(33, "10 A", EstiloDeTexto.Texto)]);
    }

    [Test]
    public async Task Texto_longo_quebra_por_palavra_sem_passar_da_largura()
    {
        var texto = string.Join(" ", Enumerable.Range(1, 40).Select(numero => $"palavra{numero}"));

        var paginas = Diagramar(Conteudo(Passo("1. Passo", new Campo("Observação", texto))));

        var linhas = Linhas(paginas).Where(linha => linha.Trechos.Any(trecho => trecho.Estilo == EstiloDeTexto.Texto)).ToList();
        await Assert.That(linhas.Count).IsGreaterThan(1);
        await Assert.That(linhas.All(linha => linha.Trechos.Max(trecho => trecho.X + trecho.Texto.Length) <= 90)).IsTrue();
        await Assert.That(linhas.Skip(1).All(linha => linha.Trechos[0].X == 22)).IsTrue();
        var reconstruido = string.Join(" ", linhas.SelectMany(linha => linha.Trechos).Where(trecho => trecho.Estilo == EstiloDeTexto.Texto).Select(trecho => trecho.Texto));
        await Assert.That(reconstruido).IsEqualTo(texto);
    }

    [Test]
    public async Task Palavra_maior_que_a_linha_e_partida_sem_perder_caractere()
    {
        var identificador = "sha256:" + new string('a', 150);

        var paginas = Diagramar(Conteudo(Passo("1. Passo", new Campo("Id", identificador, EhCodigo: true))));

        var pedacos = Linhas(paginas).SelectMany(linha => linha.Trechos).Where(trecho => trecho.Estilo == EstiloDeTexto.Codigo).ToList();
        await Assert.That(string.Concat(pedacos.Select(pedaco => pedaco.Texto))).IsEqualTo(identificador);
        await Assert.That(pedacos.All(pedaco => pedaco.X + pedaco.Texto.Length <= 90)).IsTrue();
    }

    [Test]
    public async Task Titulo_do_passo_nunca_fica_sozinho_no_pe_da_pagina()
    {
        for (var campos = 1; campos <= 15; campos++)
        {
            var primeiro = Passo("1. Primeiro", Enumerable.Range(1, campos).Select(numero => new Campo("C", $"{numero}")).ToArray());

            var paginas = Diagramar(Conteudo(primeiro, Passo("2. Segundo", new Campo("C", "x")), Passo("3. Terceiro", new Campo("C", "y"))));

            foreach (var pagina in paginas)
                await Assert.That(pagina.Linhas[^1].Trechos[0].Estilo).IsNotEqualTo(EstiloDeTexto.TituloDoPasso);
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task Passo_que_cabe_numa_pagina_nunca_e_dividido(int camposPorPasso)
    {
        var passos = Enumerable.Range(1, 20)
            .Select(numero => Passo($"{numero}. Passo", Enumerable.Range(1, camposPorPasso).Select(campo => new Campo("C", $"{numero}.{campo}")).ToArray()))
            .ToArray();

        var paginas = Diagramar(Conteudo(passos));

        await Assert.That(paginas.Count).IsGreaterThan(1);
        var paginaDoTexto = paginas
            .SelectMany((pagina, indice) => pagina.Linhas.SelectMany(linha => linha.Trechos).Select(trecho => (trecho.Texto, Pagina: indice)))
            .ToLookup(par => par.Texto, par => par.Pagina);
        foreach (var numero in Enumerable.Range(1, 20))
        {
            var textos = Enumerable.Range(1, camposPorPasso).Select(campo => $"{numero}.{campo}").Append($"{numero}. Passo");
            await Assert.That(textos.SelectMany(texto => paginaDoTexto[texto]).Distinct().Count()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Passo_maior_que_a_pagina_se_divide_com_o_titulo_junto_da_primeira_linha()
    {
        var enorme = Passo("1. Enorme", Enumerable.Range(1, 30).Select(numero => new Campo("C", $"{numero}")).ToArray());

        var paginas = Diagramar(Conteudo(enorme));

        await Assert.That(paginas.Count).IsGreaterThan(1);
        var primeira = paginas[0].Linhas.SelectMany(linha => linha.Trechos).Select(trecho => trecho.Texto).ToList();
        await Assert.That(primeira).Contains("1. Enorme");
        await Assert.That(primeira).Contains("C:");
    }

    [Test]
    public async Task Rodape_numera_as_paginas_e_nenhuma_linha_passa_do_limite()
    {
        var passos = Enumerable.Range(1, 12).Select(numero => Passo($"{numero}. Passo", new Campo("Resultado", "1 A"), new Campo("Referência", "Tabela"))).ToArray();

        var paginas = Diagramar(Conteudo(passos));

        await Assert.That(paginas.Count).IsGreaterThan(1);
        for (var indice = 0; indice < paginas.Count; indice++)
        {
            await Assert.That(paginas[indice].Rodape.Trechos[0].Texto).IsEqualTo($"Relatório de teste · página {indice + 1} de {paginas.Count}");
            await Assert.That(paginas[indice].Linhas.All(linha => linha.Y + 10 <= 180)).IsTrue();
        }
    }

    [Test]
    public async Task Nenhum_passo_nem_campo_se_perde_entre_paginas()
    {
        var passos = Enumerable.Range(1, 20).Select(numero => Passo($"{numero}. Passo", new Campo("Resultado", $"{numero} A"))).ToArray();

        var textos = Linhas(Diagramar(Conteudo(passos))).SelectMany(linha => linha.Trechos).Select(trecho => trecho.Texto).ToList();

        await Assert.That(textos.Where(texto => texto.EndsWith(". Passo"))).IsEquivalentTo(passos.Select(passo => passo.Titulo));
        await Assert.That(textos.Count(texto => texto == "Resultado:")).IsEqualTo(20);
    }

    private static IReadOnlyList<PaginaDiagramada> Diagramar(ConteudoDoRelatorio conteudo) =>
        new DiagramacaoDoRelatorio(new MedidorDeTeste(), Pequena).Diagramar(conteudo);

    private static IEnumerable<LinhaDiagramada> Linhas(IReadOnlyList<PaginaDiagramada> paginas) => paginas.SelectMany(pagina => pagina.Linhas);

    private static ConteudoDoRelatorio Conteudo(params SecaoDePasso[] passos) => new("Relatório de teste", [], "nota", [], passos);

    private static SecaoDePasso Passo(string titulo, params Campo[] campos) => new(titulo, campos);

    private sealed class MedidorDeTeste : IMedidorDeTexto
    {
        public double Largura(string texto, EstiloDeTexto estilo) => new StringInfo(texto).LengthInTextElements;

        public double AlturaDaLinha(EstiloDeTexto estilo) => 10;
    }
}
