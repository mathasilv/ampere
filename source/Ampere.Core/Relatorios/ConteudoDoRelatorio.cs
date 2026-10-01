using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;

namespace Ampere.Core.Relatorios;

/// <summary>Um campo do relatório: rótulo e texto; <see cref="EhCodigo" /> para expressões e identificadores.</summary>
internal sealed record Campo(string Rotulo, string Texto, bool EhCodigo = false);

/// <summary>Um passo da memória, já com o texto de cada campo pronto para leitura.</summary>
internal sealed record SecaoDePasso(string Titulo, IReadOnlyList<Campo> Campos);

/// <summary>
///     O que o relatório diz, sem formato: o Markdown e o PDF desenham o mesmo conteúdo. Os textos são simples (sem
///     marcação); cada formato escapa ou mede o que precisar.
/// </summary>
internal sealed record ConteudoDoRelatorio(
    string Titulo,
    IReadOnlyList<Campo> Cabecalho,
    string Nota,
    IReadOnlyList<SecaoDePasso> Abertura,
    IReadOnlyList<SecaoDePasso> Passos)
{
    private const string NotaDeArredondamento =
        "Valores arredondados só para leitura: até 4 casas decimais (abaixo de 1, quatro algarismos significativos). " +
        "O documento JSON da memória guarda os valores completos.";

    // Unidades de contagem que o motor escreve no plural.
    private static readonly Dictionary<string, string> Singulares = new(StringComparer.Ordinal)
    {
        ["condutores"] = "condutor",
        ["pontos"] = "ponto",
        ["aparelhos"] = "aparelho"
    };

    public static ConteudoDoRelatorio De(MemoriaDeCalculo memoria, string? identificadorGravado) =>
        Montar(memoria, identificadorGravado, $"Memória de cálculo — circuito {memoria.Circuito}", []);

    /// <summary>Relatório do quadro de cargas: os circuitos e os totais antes dos passos da memória.</summary>
    /// <exception cref="InvalidOperationException">Quadro incompleto (sem memória) não gera relatório.</exception>
    /// <param name="fases">Cargas por fase (indicador, fora da memória): seção própria antes das pendências.</param>
    /// <param name="sugestao">Distribuição de fases sugerida (indicador, fora da memória): seção depois das cargas por fase.</param>
    public static ConteudoDoRelatorio DeQuadro(ResultadoDoQuadroDeCargas quadro, BalancoDasFases? fases = null, SugestaoDeFases? sugestao = null)
    {
        if (quadro.Memoria is not { } memoria)
            throw new InvalidOperationException($"O quadro {quadro.Nome} está incompleto (sem fator para algum tipo): relatório só de quadro montado.");

        var campos = quadro.Linhas.Select(LinhaDoQuadro).Append(new Campo("Total do quadro", TotalDoQuadro(quadro))).ToList();
        var abertura = new List<SecaoDePasso> { new("Circuitos do quadro", campos) };
        if (fases is not null) abertura.Add(new SecaoDePasso("Cargas por fase", CamposDasFases(fases)));
        if (sugestao is not null) abertura.Add(new SecaoDePasso("Distribuição de fases sugerida", CamposDaSugestao(sugestao)));
        if (quadro.Problemas.Count > 0)
            abertura.Add(new SecaoDePasso("Pendências", quadro.Problemas.Select(problema => new Campo("Aviso", problema)).ToList()));
        return Montar(memoria, null, $"Memória de cálculo — quadro {quadro.Nome}", abertura);
    }

    /// <summary>Relatório da demanda da entrada: o documento da distribuidora e as parcelas antes dos passos da memória.</summary>
    /// <exception cref="InvalidOperationException">Demanda sem cálculo (sem memória) não gera relatório.</exception>
    public static ConteudoDoRelatorio DeDemanda(Demanda.ResultadoDaDemanda demanda, Demanda.PerfilDeDemanda perfil)
    {
        if (demanda.Memoria is not { } memoria)
            throw new InvalidOperationException("A demanda não foi calculada (veja os problemas): relatório só de demanda calculada.");

        var parcelas = demanda.Parcelas
            .Select(parcela => new Campo($"{parcela.Codigo} — {parcela.Descricao}",
                $"{Contagem(parcela.Pontos, "ponto", "pontos")} · instalada {Quantidade(parcela.PotenciaInstaladaVA, "VA")} · demanda {Quantidade(parcela.DemandaVA, "VA")}"))
            .Append(new Campo("Total", $"instalada {Quantidade(demanda.PotenciaInstaladaVA, "VA")} · demanda {Quantidade(demanda.DemandaVA!.Value, "VA")}"))
            .ToList();
        var abertura = new List<SecaoDePasso>
        {
            new("Documento da distribuidora", [new Campo("Fonte", perfil.Fonte), new Campo("Situação", perfil.Situacao)]),
            new("Parcelas", parcelas)
        };
        return Montar(memoria, null, $"Memória de cálculo — {memoria.Circuito.ToLowerInvariant()} ({perfil.Nome})", abertura,
            "Documento da distribuidora", "Identificador da memória (não gravado no modelo)");
    }

    /// <summary>Relatório do padrão de entrada: o documento da distribuidora e a linha da tabela antes dos passos da memória.</summary>
    /// <exception cref="InvalidOperationException">Padrão que não saiu (sem memória) não gera relatório.</exception>
    public static ConteudoDoRelatorio DePadrao(Demanda.ResultadoDoPadrao padrao, Demanda.NormaDoPadraoDeEntrada norma)
    {
        if (padrao.Memoria is not { } memoria)
            throw new InvalidOperationException("O padrão de entrada não saiu (veja os problemas): relatório só de padrão dimensionado.");

        var abertura = new List<SecaoDePasso>
        {
            new("Documento da distribuidora", [new Campo("Fonte", norma.Fonte), new Campo("Situação", norma.Situacao)]),
            new("Escolha", [new Campo("Tabela", padrao.Tabela!.Descricao), new Campo("Fornecimento", padrao.Fornecimento!.Nome)])
        };
        return Montar(memoria, null, $"Memória de cálculo — padrão de entrada ({norma.Nome})", abertura,
            "Documento da distribuidora", "Identificador da memória (não gravado no modelo)");
    }

    /// <summary>
    ///     Relatório dos DPS do quadro: o quadro e as escolhas, a localização e o que conferir no catálogo do DPS antes dos
    ///     passos da memória.
    /// </summary>
    /// <exception cref="InvalidOperationException">Seleção que não saiu (sem memória) não gera relatório.</exception>
    public static ConteudoDoRelatorio DeDps(Surtos.ResultadoDoDps dps, Surtos.NormaDeDps norma)
    {
        if (dps.Memoria is not { } memoria)
            throw new InvalidOperationException("Os DPS não foram selecionados (veja os problemas): relatório só de seleção feita.");

        var quadro = dps.Quadro;
        var tensoes = quadro.TensaoEntreFasesV is { } entreFases && quadro.Esquema != "F+N"
            ? $"{Quantidade(entreFases, string.Empty)}/{Quantidade(quadro.TensaoFaseNeutroV!.Value, "V")}"
            : Quantidade(quadro.TensaoFaseNeutroV!.Value, "V");
        var escolha = dps.Escolha;
        var localizacao = new List<Campo> { new("Referência", norma.ReferenciaDaLocalizacao) };
        if (escolha.Finalidade is not Surtos.FinalidadeDoDps.DescargasDiretas)
            localizacao.Add(new Campo("Sobretensões transmitidas pela linha externa e de manobra", norma.LocalizacaoDaLinhaExterna));
        if (escolha.Finalidade is not Surtos.FinalidadeDoDps.LinhaExterna)
            localizacao.Add(new Campo("Descargas atmosféricas diretas", norma.LocalizacaoDasDescargasDiretas));
        var abertura = new List<SecaoDePasso>
        {
            new("Quadro", [
                new Campo("Quadro", quadro.Nome),
                new Campo("Alimentação", $"{quadro.Esquema} {tensoes} ({quadro.Origem})"),
                new Campo("Esquema de aterramento", escolha.EsquemaDeAterramento),
                new Campo("Finalidade", escolha.Finalidade switch
                {
                    Surtos.FinalidadeDoDps.LinhaExterna => "sobretensões de origem atmosférica transmitidas pela linha externa e de manobra",
                    Surtos.FinalidadeDoDps.DescargasDiretas => "descargas atmosféricas diretas sobre a edificação ou em suas proximidades",
                    _ => "as duas: sobretensões transmitidas pela linha externa e de manobra, e descargas atmosféricas diretas"
                }),
                new Campo("Dispositivo DR", escolha.AJusanteDeDr ? "os DPS ficam a jusante de um DR" : "os DPS ficam a montante dos DR")
            ]),
            new("Localização", localizacao),
            new("A conferir no DPS escolhido", norma.AConferir.Select(item => new Campo(item.Referencia, item.Texto)).ToList())
        };
        return Montar(memoria, null, $"Memória de cálculo — {memoria.Circuito}", abertura, rotuloDoIdentificador: "Identificador da memória (não gravado no modelo)");
    }

    // Memória que não vai para o modelo (ex.: a demanda) não tem AMP_MemoriaCalculoId: os rótulos dizem o que ela é.
    private static ConteudoDoRelatorio Montar(
        MemoriaDeCalculo memoria, string? identificadorGravado, string titulo, IReadOnlyList<SecaoDePasso> abertura,
        string rotuloDoPerfil = "Perfil normativo", string rotuloDoIdentificador = "Identificador (AMP_MemoriaCalculoId)")
    {
        var identificador = memoria.Hash();
        var cabecalho = new List<Campo>
        {
            new(rotuloDoPerfil, memoria.PerfilNorma),
            new(rotuloDoIdentificador, identificador, EhCodigo: true),
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
        return new ConteudoDoRelatorio(titulo, cabecalho, NotaDeArredondamento, abertura, passos);
    }

    private static Campo LinhaDoQuadro(LinhaDoQuadroDeCargas linha) =>
        new($"{linha.Numero} ({CodigosDeTipoDeCarga.Codigo(linha.Tipo)})", linha.DemandaVA is { } demanda
            ? $"instalada {Quantidade(linha.PotenciaInstaladaVA, "VA")} · fd {Quantidade(linha.Fator!.Value, string.Empty)} · demanda {Quantidade(demanda, "VA")}"
            : $"instalada {Quantidade(linha.PotenciaInstaladaVA, "VA")} · sem fator");

    private static List<Campo> CamposDasFases(BalancoDasFases balanco)
    {
        var campos = balanco.Fases
            .Select(fase => new Campo($"Fase {fase.Fase}",
                $"instalada {Quantidade(fase.PotenciaInstaladaVA, "VA")}"
                + (fase.DemandaVA is { } demanda ? $" · demanda {Quantidade(demanda, "VA")}" : string.Empty)
                + (fase.CorrenteA is { } corrente ? $" · corrente {Quantidade(corrente, "A")}" : string.Empty)))
            .ToList();
        campos.Add(new Campo("Desequilíbrio",
            $"{Quantidade(balanco.DesequilibrioPct, "%")} = (maior − menor) / maior, pela {(balanco.PelaDemanda ? "demanda" : "potência instalada")}; " +
            $"fase mais carregada: {balanco.FaseMaisCarregada}. Indicador para distribuir os circuitos: a NBR 5410 não fixa limite"));
        if (balanco.CircuitosSemFase.Count > 0)
            campos.Add(new Campo("Sem fase identificada (fora da conta)", string.Join(", ", balanco.CircuitosSemFase)));
        return campos;
    }

    private static List<Campo> CamposDaSugestao(SugestaoDeFases sugestao)
    {
        string Correntes(IReadOnlyList<decimal> correntes) =>
            string.Join(" · ", sugestao.Fases.Select((fase, indice) => $"{fase} {Quantidade(correntes[indice], "A")}"));

        var semFase = sugestao.Mudancas.Any(mudanca => mudanca.Atuais is null);
        var campos = new List<Campo>
        {
            new("Hoje", Correntes(sugestao.CorrentesAntesA) + (semFase ? " (sem os circuitos sem fase identificada)" : string.Empty)),
            new("Sugerida", Correntes(sugestao.CorrentesDepoisA))
        };
        campos.AddRange(sugestao.Mudancas.Select(mudanca =>
            new Campo(mudanca.Numero, $"{(mudanca.Atuais is { } atuais ? string.Join(",", atuais) : "sem fase")} → {string.Join(",", mudanca.Sugeridas)}")));
        if (sugestao.Mudancas.Count == 0) campos.Add(new Campo("Mudanças", "nenhuma: a distribuição de hoje já é a melhor que o Ampere acha"));
        campos.Add(new Campo("Critério",
            $"corrente de cada fase (soma das correntes de linha dos circuitos nela, pela {(sugestao.PelaDemanda ? "demanda" : "potência instalada")}): " +
            "a maior primeiro, depois a diferença entre as fases; só mudanças que valem o trabalho (0,1 A na maior, ou 1% na soma dos quadrados). " +
            "Indicador: o Ampere não muda as fases no modelo; mova os circuitos no quadro do Revit e monte o quadro de novo"));
        return campos;
    }

    private static string TotalDoQuadro(ResultadoDoQuadroDeCargas quadro)
    {
        var texto = $"instalada {Quantidade(quadro.PotenciaInstaladaVA, "VA")}";
        texto += quadro.DemandaVA is { } demanda ? $" · demanda {Quantidade(demanda, "VA")}" : " · demanda incompleta";
        texto += quadro.CorrenteDeDemandaA is { } corrente ? $" · corrente {Quantidade(corrente, "A")}" : " · corrente não calculada";
        return texto;
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
