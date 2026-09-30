using System.Globalization;
using System.Text;

namespace Ampere.Core.Relatorios.Pdf;

/// <summary>Papel de cada texto na página; o desenho escolhe fonte e cor por estilo.</summary>
internal enum EstiloDeTexto
{
    Titulo,
    Secao,
    TituloDoPasso,
    Rotulo,
    Texto,
    Codigo,
    Nota,
    Rodape
}

/// <summary>Mede texto numa fonte; o PDF usa a métrica real, os testes um medidor falso.</summary>
internal interface IMedidorDeTexto
{
    double Largura(string texto, EstiloDeTexto estilo);

    double AlturaDaLinha(EstiloDeTexto estilo);
}

/// <summary>Geometria da folha, em pontos (1/72 pol.).</summary>
internal sealed record Folha(double Largura, double Altura, double Margem, double FaixaDoRodape)
{
    /// <summary>A4 como o PDFsharp a define, margens de 2 cm.</summary>
    public static Folha A4 { get; } = new(595, 842, 56.7, 24);

    public double LarguraUtil => Largura - 2 * Margem;

    public double LimiteDoConteudo => Altura - Margem - FaixaDoRodape;
}

/// <summary>Texto num estilo, a partir de X (a linha dá o Y do topo).</summary>
internal sealed record Trecho(double X, string Texto, EstiloDeTexto Estilo);

internal sealed record LinhaDiagramada(double Y, IReadOnlyList<Trecho> Trechos);

internal sealed record PaginaDiagramada(IReadOnlyList<LinhaDiagramada> Linhas, LinhaDiagramada Rodape);

/// <summary>
///     Distribui o conteúdo do relatório em páginas: quebra de linha por palavra (palavra maior que a linha é partida),
///     cada passo inteiro numa página (só um passo maior que a página se divide, e aí o título vai com a primeira linha)
///     e rodapé "página X de N". Sem nada do PDFsharp: só medidas.
/// </summary>
internal sealed class DiagramacaoDoRelatorio(IMedidorDeTexto medidor, Folha folha)
{
    private const double RecuoDosCampos = 12;
    private const double Tolerancia = 0.001;

    private readonly List<List<LinhaDiagramada>> _paginas = [[]];
    private double _y;

    public IReadOnlyList<PaginaDiagramada> Diagramar(ConteudoDoRelatorio conteudo)
    {
        _y = folha.Margem;
        Paragrafo([(conteudo.Titulo, EstiloDeTexto.Titulo)], 0);
        Espaco(6);
        foreach (var campo in conteudo.Cabecalho) Paragrafo(Trechos(campo), 0);
        Espaco(8);
        Paragrafo([(conteudo.Nota, EstiloDeTexto.Nota)], 0);
        Espaco(10);
        Secao(conteudo.Abertura);
        Paragrafo([("Passos", EstiloDeTexto.Secao)], 0);
        Secao(conteudo.Passos);

        var total = _paginas.Count;
        return _paginas.Select((linhas, indice) => new PaginaDiagramada(linhas, Rodape(conteudo.Titulo, indice + 1, total))).ToList();
    }

    private void Secao(IEnumerable<SecaoDePasso> secoes)
    {
        foreach (var passo in secoes)
        {
            Espaco(8);
            var titulo = Quebrar([(passo.Titulo, EstiloDeTexto.TituloDoPasso)], folha.Margem, folha.LarguraUtil);
            var campos = passo.Campos.Select(campo => Quebrar(Trechos(campo), folha.Margem + RecuoDosCampos, folha.LarguraUtil - RecuoDosCampos)).ToList();
            var inteiro = Altura(titulo) + 2 + campos.Sum(Altura);
            var necessario = inteiro <= folha.LimiteDoConteudo - folha.Margem ? inteiro : Altura(titulo) + 2 + Altura(campos[0].Take(1));
            if (!Cabe(necessario)) NovaPagina();
            Colocar(titulo);
            Espaco(2);
            foreach (var campo in campos) Colocar(campo);
        }
    }

    private static List<(string Texto, EstiloDeTexto Estilo)> Trechos(Campo campo) =>
        [($"{campo.Rotulo}: ", EstiloDeTexto.Rotulo), (campo.Texto, campo.EhCodigo ? EstiloDeTexto.Codigo : EstiloDeTexto.Texto)];

    private void Paragrafo(IReadOnlyList<(string Texto, EstiloDeTexto Estilo)> trechos, double recuo) =>
        Colocar(Quebrar(trechos, folha.Margem + recuo, folha.LarguraUtil - recuo));

    private void Colocar(IEnumerable<List<Trecho>> linhas)
    {
        foreach (var linha in linhas)
        {
            var altura = AlturaDaLinha(linha);
            if (!Cabe(altura)) NovaPagina();
            _paginas[^1].Add(new LinhaDiagramada(_y, linha));
            _y += altura;
        }
    }

    private double Altura(IEnumerable<List<Trecho>> linhas) => linhas.Sum(AlturaDaLinha);

    private double AlturaDaLinha(List<Trecho> linha) => linha.Max(trecho => medidor.AlturaDaLinha(trecho.Estilo));

    // Espaço entre blocos; no topo da página não se acumula.
    private void Espaco(double pontos)
    {
        if (_paginas[^1].Count > 0) _y += pontos;
    }

    // Página vazia aceita qualquer linha: evita laço infinito com uma linha maior que a página.
    private bool Cabe(double altura) => _paginas[^1].Count == 0 || _y + altura <= folha.LimiteDoConteudo + Tolerancia;

    private void NovaPagina()
    {
        _paginas.Add([]);
        _y = folha.Margem;
    }

    private List<List<Trecho>> Quebrar(IReadOnlyList<(string Texto, EstiloDeTexto Estilo)> trechos, double inicio, double largura)
    {
        var linhas = new List<List<Trecho>>();
        var atual = new List<(double X, string Texto, EstiloDeTexto Estilo, bool EspacoAntes)>();
        var x = inicio;
        foreach (var (palavra, estilo, espacoAntes) in Palavras(trechos))
        {
            var primeiro = true;
            foreach (var pedaco in Partir(palavra, estilo, largura))
            {
                var comEspaco = primeiro && espacoAntes && atual.Count > 0;
                var espaco = comEspaco ? medidor.Largura(" ", estilo) : 0;
                var larguraDoPedaco = medidor.Largura(pedaco, estilo);
                if (atual.Count > 0 && x + espaco + larguraDoPedaco > inicio + largura + Tolerancia)
                {
                    linhas.Add(Juntar(atual));
                    atual = [];
                    x = inicio;
                    espaco = 0;
                    comEspaco = false;
                }

                atual.Add((x + espaco, pedaco, estilo, comEspaco));
                x += espaco + larguraDoPedaco;
                primeiro = false;
            }
        }

        if (atual.Count > 0) linhas.Add(Juntar(atual));
        return linhas;
    }

    // Palavras vizinhas do mesmo estilo viram um trecho só (a largura é aditiva: sem kerning no PDFsharp).
    private static List<Trecho> Juntar(List<(double X, string Texto, EstiloDeTexto Estilo, bool EspacoAntes)> palavras)
    {
        var trechos = new List<Trecho>();
        foreach (var palavra in palavras)
        {
            if (trechos.Count > 0 && trechos[^1].Estilo == palavra.Estilo)
                trechos[^1] = trechos[^1] with { Texto = trechos[^1].Texto + (palavra.EspacoAntes ? " " : string.Empty) + palavra.Texto };
            else
                trechos.Add(new Trecho(palavra.X, palavra.Texto, palavra.Estilo));
        }

        return trechos;
    }

    // Palavras de todos os trechos, em ordem; espaço, tabulação e quebra de linha separam palavras.
    private static IEnumerable<(string Palavra, EstiloDeTexto Estilo, bool EspacoAntes)> Palavras(IReadOnlyList<(string Texto, EstiloDeTexto Estilo)> trechos)
    {
        var espacoAntes = false;
        var palavra = new StringBuilder();
        foreach (var (texto, estilo) in trechos)
        {
            foreach (var caractere in texto.Normalize(NormalizationForm.FormC))
            {
                if (char.IsWhiteSpace(caractere))
                {
                    if (palavra.Length > 0)
                    {
                        yield return (palavra.ToString(), estilo, espacoAntes);
                        palavra.Clear();
                    }

                    espacoAntes = true;
                }
                else
                {
                    palavra.Append(caractere);
                }
            }

            if (palavra.Length > 0)
            {
                yield return (palavra.ToString(), estilo, espacoAntes);
                palavra.Clear();
                espacoAntes = false;
            }
        }
    }

    // Palavra maior que a linha é partida por caractere (sem separar pares substitutos nem acentos combinados).
    private IEnumerable<string> Partir(string palavra, EstiloDeTexto estilo, double largura)
    {
        if (medidor.Largura(palavra, estilo) <= largura + Tolerancia)
        {
            yield return palavra;
            yield break;
        }

        var inicios = StringInfo.ParseCombiningCharacters(palavra);
        var atual = 0;
        while (atual < inicios.Length)
        {
            var fim = atual + 1;
            while (fim < inicios.Length && medidor.Largura(palavra[inicios[atual]..FimDoElemento(inicios, fim, palavra.Length)], estilo) <= largura + Tolerancia)
                fim++;
            yield return palavra[inicios[atual]..FimDoElemento(inicios, fim - 1, palavra.Length)];
            atual = fim;
        }
    }

    private static int FimDoElemento(int[] inicios, int indice, int comprimento) => indice + 1 < inicios.Length ? inicios[indice + 1] : comprimento;

    private LinhaDiagramada Rodape(string titulo, int pagina, int total)
    {
        var texto = $"{titulo} · página {pagina} de {total}";
        var x = folha.Margem + Math.Max(0, (folha.LarguraUtil - medidor.Largura(texto, EstiloDeTexto.Rodape)) / 2);
        var y = folha.Altura - folha.Margem - medidor.AlturaDaLinha(EstiloDeTexto.Rodape);
        return new LinhaDiagramada(y, [new Trecho(x, texto, EstiloDeTexto.Rodape)]);
    }
}
