using Ampere.Core.Catalogos;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Relatorios;

/// <summary>Um campo do relatório: rótulo e texto; <see cref="EhCodigo" /> para expressões e identificadores.</summary>
internal sealed record Campo(string Rotulo, string Texto, bool EhCodigo = false);

/// <summary>Um passo da memória, já com o texto de cada campo pronto para leitura.</summary>
internal sealed record SecaoDePasso(string Titulo, IReadOnlyList<Campo> Campos);

/// <summary>
///     O que o relatório diz, sem formato: o Markdown e o PDF desenham o mesmo conteúdo. Os textos são simples (sem
///     marcação); cada formato escapa ou mede o que precisar.
/// </summary>
internal sealed record ConteudoDoRelatorio(string Titulo, IReadOnlyList<Campo> Cabecalho, string Nota, IReadOnlyList<SecaoDePasso> Passos)
{
    private const string NotaDeArredondamento =
        "Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). " +
        "O documento JSON da memória guarda os valores completos.";

    // Unidades de contagem que o motor escreve no plural.
    private static readonly Dictionary<string, string> Singulares = new(StringComparer.Ordinal)
    {
        ["condutores"] = "condutor",
        ["pontos"] = "ponto"
    };

    public static ConteudoDoRelatorio De(MemoriaDeCalculo memoria, string? identificadorGravado)
    {
        var identificador = memoria.Hash();
        var cabecalho = new List<Campo>
        {
            new("Perfil normativo", memoria.PerfilNorma),
            new("Identificador (AMP_MemoriaCalculoId)", identificador, EhCodigo: true),
            new("Esquema do documento", MemoriaDeCalculo.VersaoDoEsquema),
            new("Situação", Situacao(memoria))
        };

        var pendentes = memoria.Passos
            .Select((passo, indice) => (Referencia: passo.Referencia.Trim(), Numero: indice + 1))
            .Where(passo => passo.Referencia is PerfilNormativo.TodoNorma or RegrasDeCatalogo.TodoCatalogo)
            .Select(passo => passo.Numero)
            .ToList();
        if (pendentes.Count > 0)
        {
            cabecalho.Add(new Campo("Referências pendentes (TODO_NORMA ou TODO_CATALOGO)",
                $"{Contagem(pendentes.Count, "passo", "passos")} ({string.Join(", ", pendentes)}), sem fonte oficial"));
        }

        if (memoria.PerfilNorma.StartsWith("FICTICIO", StringComparison.Ordinal))
            cabecalho.Add(new Campo("Atenção", "perfil fictício, só para testes"));
        if (identificadorGravado is not null && identificadorGravado.Trim() != identificador)
            cabecalho.Add(new Campo("Atenção", $"o identificador gravado no elemento ({identificadorGravado.Trim()}) não confere com esta memória"));

        var passos = memoria.Passos.Select((passo, indice) => new SecaoDePasso($"{indice + 1}. {passo.Descricao}", Campos(passo))).ToList();
        return new ConteudoDoRelatorio($"Memória de cálculo — circuito {memoria.Circuito}", cabecalho, NotaDeArredondamento, passos);
    }

    private static List<Campo> Campos(PassoDeCalculo passo)
    {
        var campos = new List<Campo>
        {
            new("Referência", passo.Referencia),
            new("Expressão", passo.Expressao, EhCodigo: true)
        };
        if (passo.Valores.Count > 0)
            campos.Add(new Campo("Valores", string.Join("; ", passo.Valores.Select(valor => $"{valor.Nome} = {Quantidade(valor.Valor, valor.Unidade)}"))));
        campos.Add(new Campo("Resultado", passo.Resultado is { } resultado ? Quantidade(resultado, passo.Unidade) : "não calculado"));
        if (passo.Observacao is not null) campos.Add(new Campo("Observação", passo.Observacao));
        return campos;
    }

    // Resultado nulo marca o passo em que o cálculo parou (PassoDeCalculo.Resultado).
    private static string Situacao(MemoriaDeCalculo memoria)
    {
        var parada = memoria.Passos.Select((passo, indice) => (passo, indice)).FirstOrDefault(par => par.passo.Resultado is null);
        if (parada.passo is null) return $"cálculo completo ({Contagem(memoria.Passos.Count, "passo", "passos")})";

        return $"cálculo interrompido no passo {parada.indice + 1} ({parada.passo.Descricao}): {parada.passo.Observacao ?? "motivo não registrado"}";
    }

    private static string Quantidade(decimal valor, string unidade)
    {
        var numero = NumeroEmTexto.FormatarParaLeitura(valor);
        if (unidade.Length == 0) return numero;
        if (unidade == "%") return numero + "%";
        return $"{numero} {(valor == 1m && Singulares.TryGetValue(unidade, out var singular) ? singular : unidade)}";
    }

    private static string Contagem(int quantidade, string singular, string plural) => $"{quantidade} {(quantidade == 1 ? singular : plural)}";
}
