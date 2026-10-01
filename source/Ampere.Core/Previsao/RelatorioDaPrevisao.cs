using System.Text;
using Ampere.Core.Relatorios;

namespace Ampere.Core.Previsao;

/// <summary>
///     Relatório da previsão de cargas: Markdown com os critérios (cada um com a referência) e a tabela dos cômodos, e a
///     planilha CSV para o Excel em português. Determinístico: mesmos cômodos, mesmo texto.
/// </summary>
public static class RelatorioDaPrevisao
{
    private static readonly string[] Cabecalho =
    [
        "Pavimento", "Cômodo", "Número", "Categoria", "Área (m²)", "Perímetro (m)", "Iluminação mínima (VA)", "Iluminação no modelo (VA)",
        "Pontos de luz", "TUG mínimas", "TUG no modelo", "Situação", "Faltas", "Observações"
    ];

    public static string Markdown(ResultadoDaPrevisao resultado)
    {
        var norma = resultado.Norma;
        var texto = new StringBuilder();
        texto.Append("# Previsão de cargas dos locais de habitação\n\n");
        texto.Append($"- **Perfil normativo:** {norma.Perfil}\n");
        texto.Append($"- **Cômodos:** {Resumo(resultado)}\n\n");

        texto.Append("## Critérios\n\n");
        texto.Append($"- **Iluminação** ({norma.ReferenciaDaIluminacao}): pelo menos {norma.PontosDeLuzMinimos} ponto de luz; " +
                     $"{N(norma.PotenciaInicialVA)} VA até {N(norma.AreaInicialM2)} m², mais {N(norma.AcrescimoVA)} VA a cada {N(norma.ACadaM2)} m² inteiros acima disso.\n");
        texto.Append($"- **Pontos de tomada** ({norma.ReferenciaDasTomadas}):\n");
        foreach (var regra in norma.Comodos)
        {
            var potencia = regra.SeiscentosVa
                ? $"{N(norma.SeiscentosVA)} VA nos {norma.PontosDeSeiscentos} primeiros pontos e {N(norma.DemaisVA)} VA nos demais"
                : $"{N(norma.DemaisVA)} VA por ponto";
            texto.Append($"  - {regra.Comodo} (alínea {regra.Alinea}): {regra.Descrever()}; {potencia}.\n");
        }

        texto.Append($"- **Potência das tomadas** ({norma.ReferenciaDaPotencia}). Pontos de tomada de uso geral no conjunto dos cômodos da potência maior: " +
                     $"{resultado.PontosNoConjunto}.\n");
        texto.Append("- Só os pontos TUG contam como tomadas; a TUE atende a um aparelho. A alternativa da ABNT NBR 5413 para a iluminação não é avaliada.\n\n");

        texto.Append("## Cômodos\n\n");
        texto.Append("| Pavimento | Cômodo | Categoria | Área (m²) | Perímetro (m) | Iluminação mín. / modelo (VA) | Pontos de luz | TUG mín. / modelo | Situação | Faltas e observações |\n");
        texto.Append("|---|---|---|---:|---:|---:|---:|---:|---|---|\n");
        foreach (var avaliacao in resultado.Comodos)
        {
            var comodo = avaliacao.Comodo;
            var notas = avaliacao.Faltas.Concat(avaliacao.Observacoes).ToList();
            texto.Append($"| {Celula(comodo.Pavimento)} | {Celula(Identificacao(comodo))} | {Celula(avaliacao.Categoria)} | {N(comodo.AreaM2)} | {N(comodo.PerimetroM)} | " +
                         $"{Talvez(avaliacao.IluminacaoMinimaVA)} / {N(avaliacao.IluminacaoNoModeloVA)} | {avaliacao.PontosDeLuz} | " +
                         $"{(avaliacao.TomadasMinimas is { } minimas ? minimas.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—")} / {avaliacao.Tomadas} | " +
                         $"{Situacao(avaliacao.Situacao)} | {Celula(string.Join("; ", notas))} |\n");
        }

        return texto.ToString();
    }

    public static string Csv(ResultadoDaPrevisao resultado)
    {
        var texto = new StringBuilder();
        CsvEmPortugues.Linha(texto, Cabecalho);
        foreach (var avaliacao in resultado.Comodos)
        {
            var comodo = avaliacao.Comodo;
            CsvEmPortugues.Linha(texto,
            [
                comodo.Pavimento, comodo.Nome, comodo.Numero, avaliacao.Categoria, N(comodo.AreaM2), N(comodo.PerimetroM),
                avaliacao.IluminacaoMinimaVA is { } minima ? N(minima) : null, N(avaliacao.IluminacaoNoModeloVA),
                avaliacao.PontosDeLuz.ToString(System.Globalization.CultureInfo.InvariantCulture),
                avaliacao.TomadasMinimas?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                avaliacao.Tomadas.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Situacao(avaliacao.Situacao), string.Join(" | ", avaliacao.Faltas), string.Join(" | ", avaliacao.Observacoes)
            ]);
        }

        return texto.ToString();
    }

    /// <summary>Contagem por situação, para o resumo e o relatório (ex.: "12 avaliados: 10 atendem, 2 não atendem; 3 sem categoria").</summary>
    public static string Resumo(ResultadoDaPrevisao resultado)
    {
        var avaliados = resultado.Contar(SituacaoDoComodo.Atende) + resultado.Contar(SituacaoDoComodo.AtendePelaAlternativa) + resultado.Contar(SituacaoDoComodo.NaoAtende);
        var partes = new List<string>
        {
            $"{avaliados} avaliado(s): {resultado.Contar(SituacaoDoComodo.Atende)} atende(m), " +
            $"{resultado.Contar(SituacaoDoComodo.AtendePelaAlternativa)} pela alternativa de potência, {resultado.Contar(SituacaoDoComodo.NaoAtende)} não atende(m)"
        };
        if (resultado.Contar(SituacaoDoComodo.SemCategoria) is > 0 and var semCategoria) partes.Add($"{semCategoria} sem categoria");
        if (resultado.Contar(SituacaoDoComodo.ForaDaHabitacao) is > 0 and var fora) partes.Add($"{fora} fora da habitação");
        if (resultado.Contar(SituacaoDoComodo.SemArea) is > 0 and var semArea) partes.Add($"{semArea} sem área");
        return string.Join("; ", partes);
    }

    public static string Situacao(SituacaoDoComodo situacao) => situacao switch
    {
        SituacaoDoComodo.Atende => "Atende",
        SituacaoDoComodo.AtendePelaAlternativa => "Atende pela alternativa",
        SituacaoDoComodo.NaoAtende => "Não atende",
        SituacaoDoComodo.SemCategoria => "Sem categoria",
        SituacaoDoComodo.ForaDaHabitacao => "Fora da habitação",
        _ => "Sem área"
    };

    private static string Identificacao(ComodoDoProjeto comodo) => comodo.Numero is { Length: > 0 } numero ? $"{comodo.Nome} ({numero})" : comodo.Nome;

    private static string Talvez(decimal? valor) => valor is { } numero ? N(numero) : "—";

    // Barra vertical quebraria a tabela; quebra de linha também.
    private static string Celula(string? texto) =>
        string.IsNullOrEmpty(texto) ? "—" : texto.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string N(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);
}
