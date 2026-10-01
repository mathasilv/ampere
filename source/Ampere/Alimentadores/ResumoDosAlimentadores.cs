using System.Text;
using Ampere.Core;
using Ampere.Core.Alimentadores;
using Ampere.Core.Dimensionamento;

namespace Ampere.Alimentadores;

/// <summary>Texto apresentado ao fim do dimensionamento dos alimentadores: uma linha por quadro, com o resultado ou o motivo.</summary>
internal static class ResumoDosAlimentadores
{
    public static string Texto(IReadOnlyList<ResultadoDoAlimentador> resultados, string origem, TimeSpan tempo, string? pasta, int gerados, IReadOnlyList<string> erros)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"Origem: {DimensionamentoDeAlimentadores.Origens[origem]}");
        texto.AppendLine();
        if (resultados.Count == 0) texto.AppendLine("Nenhum quadro com circuitos no projeto.");

        foreach (var resultado in resultados)
        {
            var calculo = resultado.Circuito?.Dimensionamento;
            if (resultado.Problemas.Count > 0 || calculo is null)
            {
                texto.AppendLine($"• {resultado.Quadro}: não dimensionado — {string.Join("; ", resultado.Problemas)}");
                continue;
            }

            var partes = new List<string> { $"IB {Numero(calculo.CorrenteDeProjetoA)} A" };
            if (calculo.SecaoMm2 is { } secao) partes.Add($"{Numero(secao)} mm²");
            if (calculo.DisjuntorA is { } disjuntor) partes.Add($"disjuntor {Numero(disjuntor)} A");
            if (calculo.QuedaDeTensaoPct is { } queda) partes.Add($"queda {Numero(queda)}%");
            var situacao = calculo.Situacao == SituacaoDoDimensionamento.Dimensionado ? string.Empty : $" — parou: {string.Join("; ", calculo.Problemas)}";
            texto.AppendLine($"• {resultado.Quadro}: {string.Join(" · ", partes)}{situacao}");
            foreach (var aviso in calculo.Avisos) texto.AppendLine($"    aviso: {aviso}");
        }

        if (pasta is { Length: > 0 })
        {
            texto.AppendLine();
            texto.AppendLine($"Memórias gravadas (JSON, Markdown e PDF): {gerados}");
            texto.AppendLine(pasta);
        }

        if (erros.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("Erros de gravação dos relatórios:");
            foreach (var erro in erros.Take(8)) texto.AppendLine($"• {erro}");
        }

        texto.AppendLine().Append($"Tempo: {tempo.TotalSeconds:0.00} s");
        return texto.ToString();
    }

    private static string Numero(decimal? valor) => valor is { } numero ? NumeroEmTexto.FormatarParaLeitura(numero) : "?";
}
