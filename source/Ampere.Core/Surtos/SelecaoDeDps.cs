using Ampere.Core.Memoria;

namespace Ampere.Core.Surtos;

/// <summary>Contra o que o DPS protege (NBR 5410, 6.3.5.2.1 e 6.3.5.2.4, alínea d).</summary>
public enum FinalidadeDoDps
{
    /// <summary>Sobretensões de origem atmosférica transmitidas pela linha externa e sobretensões de manobra: In.</summary>
    LinhaExterna,

    /// <summary>Descargas atmosféricas diretas sobre a edificação ou em suas proximidades: Iimp.</summary>
    DescargasDiretas,

    /// <summary>As duas: In e Iimp, cada um pela sua regra.</summary>
    Ambas
}

/// <summary>O quadro onde vão os DPS (ponto de entrada ou quadro de distribuição principal), lido do documento.</summary>
/// <param name="Esquema">F+N, 2F, 2F+N, 3F ou 3F+N, do sistema de distribuição; nulo = o quadro não tem um.</param>
/// <param name="TensaoFaseNeutroV">Uo: a fase-terra do sistema de distribuição.</param>
/// <param name="TensaoEntreFasesV">U: a fase-fase; nula em F+N.</param>
/// <param name="Origem">De onde vieram os valores, para o relatório.</param>
public sealed record QuadroParaDps(long Id, string Nome, string? Esquema, decimal? TensaoFaseNeutroV, decimal? TensaoEntreFasesV, string Origem);

/// <summary>Porta para os quadros do documento na seleção dos DPS (implementada pelo adapter Revit).</summary>
public interface IDocumentoDeDps
{
    /// <summary>Quadros do documento (equipamentos com sistema de distribuição ou com circuitos), em ordem de nome.</summary>
    IReadOnlyList<QuadroParaDps> LerQuadros();
}

/// <summary>O que o projetista decide para os DPS do quadro.</summary>
/// <param name="EsquemaDeConexao">2 ou 3, quando a Figura 13 admite os dois; nos outros casos a norma decide.</param>
/// <param name="AJusanteDeDr">Os DPS ficam depois de um DR (DR geral antes deles).</param>
public sealed record EscolhaDoDps(string EsquemaDeAterramento, FinalidadeDoDps Finalidade, int? EsquemaDeConexao = null, bool AJusanteDeDr = false);

/// <summary>Os DPS de uma ligação (ex.: 3 entre fase e PE) e os mínimos de cada um.</summary>
public sealed record LigacaoDoDps(string Ligacao, int Quantidade, decimal UcMinimoV, decimal? InMinimoKa, decimal? IimpMinimoKa);

/// <summary>Seleção dos DPS do quadro: o esquema de conexão, as ligações, a memória e o que impediu.</summary>
public sealed record ResultadoDoDps(
    QuadroParaDps Quadro,
    EscolhaDoDps Escolha,
    int? EsquemaDeConexao,
    IReadOnlyList<LigacaoDoDps> Ligacoes,
    decimal? UpMaximoKv,
    decimal? SecaoDoCondutorDeConexaoMm2,
    MemoriaDeCalculo? Memoria,
    IReadOnlyList<string> Problemas)
{
    /// <summary>O resultado em poucas linhas, para o resumo do comando (ou os problemas, se não saiu).</summary>
    public string Resumo()
    {
        if (Memoria is null) return $"{Quadro.Nome}: os DPS não foram selecionados.\n" + string.Join("\n", Problemas.Select(problema => $"• {problema}"));

        static string N(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);
        var linhas = new List<string> { $"{Quadro.Nome}: esquema de conexão {EsquemaDeConexao} (Figura 13), {Ligacoes.Sum(ligacao => ligacao.Quantidade)} DPS" };
        linhas.AddRange(Ligacoes.Select(ligacao =>
            $"• {ligacao.Quantidade} × {LigacoesDoDps.Descrever(ligacao.Ligacao)}: Uc ≥ {N(ligacao.UcMinimoV)} V"
            + (ligacao.InMinimoKa is { } corrente ? $" · In ≥ {N(corrente)} kA" : string.Empty)
            + (ligacao.IimpMinimoKa is { } impulso ? $" · Iimp ≥ {N(impulso)} kA" : string.Empty)));
        linhas.Add($"Up ≤ {N(UpMaximoKv!.Value)} kV · condutor de conexão ao PE ≥ {N(SecaoDoCondutorDeConexaoMm2!.Value)} mm² de cobre");
        return string.Join("\n", linhas);
    }
}

/// <summary>
///     Caso de uso da seleção dos DPS da linha de energia no ponto de entrada ou no quadro de distribuição principal
///     (NBR 5410, 6.3.5.2): esquema de conexão (Figura 13), Uc (Tabela 49), Up (Tabela 31, categoria II), In e Iimp
///     (6.3.5.2.4-d), corrente subsequente do neutro–PE (6.3.5.2.4-e), condutor de conexão (6.3.5.2.9) e a imunidade do DR
///     a montante (6.3.5.2.6-b), numa memória de cálculo. Não grava nada no modelo.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>TN-C-S: entra TN-C e passa a TN-S no quadro principal (Figura 13, nota b): esquema 1, coluna TN-C da Tabela 49.</item>
///         <item>Rede com duas fases: a norma só distingue redes trifásicas e monofásicas para o neutro–PE do esquema 3; o
///         Ampere usa a trifásica (o valor maior).</item>
///         <item>Iimp: o mínimo da norma para quando a corrente não puder ser determinada pela IEC 61312-1.</item>
///         <item>O que depende do produto (sobretensões temporárias, curto-circuito, coordenação, proteção contra falha,
///         indicação do estado) vai para o relatório como lista a conferir.</item>
///     </list>
/// </remarks>
public static class SelecaoDeDps
{
    public const string Nome = "DPS do quadro";

    public static ResultadoDoDps Selecionar(QuadroParaDps quadro, EscolhaDoDps escolha, NormaDeDps norma)
    {
        var problemas = new List<string>();
        var aterramento = escolha.EsquemaDeAterramento?.Trim() ?? string.Empty;
        if (!EsquemasDeAterramento.Todos.Contains(aterramento, StringComparer.Ordinal))
            problemas.Add($"esquema de aterramento '{escolha.EsquemaDeAterramento}' fora de {string.Join(", ", EsquemasDeAterramento.Todos)}");

        var (fases, neutro) = quadro.Esquema switch
        {
            "F+N" => (1, true),
            "2F" => (2, false),
            "2F+N" => (2, true),
            "3F" => (3, false),
            "3F+N" => (3, true),
            _ => (0, false)
        };
        if (fases == 0)
            problemas.Add(quadro.Esquema is null
                ? $"quadro {quadro.Nome} sem sistema de distribuição: atribua um no Revit"
                : $"quadro {quadro.Nome} com alimentação '{quadro.Esquema}' fora de F+N, 2F, 2F+N, 3F e 3F+N");
        if (aterramento == EsquemasDeAterramento.ItSemNeutro && neutro)
            problemas.Add($"quadro {quadro.Nome} com neutro ({quadro.Esquema}) no esquema IT sem neutro distribuído");
        if (quadro.TensaoFaseNeutroV is not > 0m)
            problemas.Add($"quadro {quadro.Nome} sem a tensão fase-neutro (Uo) no sistema de distribuição");
        if (aterramento == EsquemasDeAterramento.ItSemNeutro && quadro.TensaoEntreFasesV is not > 0m)
            problemas.Add($"quadro {quadro.Nome} sem a tensão entre fases (U), que o Uc do esquema IT sem neutro pede");
        var linha31 = quadro.TensaoFaseNeutroV is > 0m and var uoLido ? norma.LinhaDaTensao(uoLido) : null;
        if (quadro.TensaoFaseNeutroV is > 0m && linha31 is null)
            problemas.Add($"tensão fase-neutro de {N(quadro.TensaoFaseNeutroV.Value)} V fora da Tabela 31 ({string.Join(", ", norma.Tabela31.SelectMany(linha => linha.Sistemas))})");
        if (problemas.Count > 0) return Falha(quadro, escolha, problemas);

        // Figura 13: sem neutro, ou com o neutro de entrada (PEN) aterrado no BEP, esquema 1; senão 2 ou 3.
        var pen = aterramento is EsquemasDeAterramento.TnC or EsquemasDeAterramento.TnCS;
        int esquema;
        string expressao;
        var observacoes = new List<string>();
        var referencia = norma.ReferenciaDoEsquemaDeConexao;
        if (!neutro || pen)
        {
            esquema = 1;
            expressao = neutro ? "neutro de entrada (PEN) aterrado no BEP → esquema 1" : "linha sem neutro → esquema 1";
            if (aterramento == EsquemasDeAterramento.TnCS)
                observacoes.Add("TN-C-S: o PEN de chegada é aterrado no BEP e separado em neutro e PE no quadro de distribuição principal (Figura 13, nota b)");
        }
        else if (aterramento == EsquemasDeAterramento.Tt && !escolha.AJusanteDeDr)
        {
            esquema = 3;
            expressao = "TT com os DPS a montante do DR → esquema 3";
            referencia = $"{referencia}; {norma.ReferenciaDoEsquema3Obrigatorio}";
        }
        else if (escolha.EsquemaDeConexao is 2 or 3)
        {
            esquema = escolha.EsquemaDeConexao.Value;
            expressao = "escolhido pelo projetista (a Figura 13 admite o 2 e o 3)";
        }
        else
        {
            return Falha(quadro, escolha, [$"quadro {quadro.Nome}: a Figura 13 admite os esquemas de conexão 2 e 3 ({aterramento} com neutro): escolha um"]);
        }

        if (escolha.EsquemaDeConexao is { } escolhido && escolhido != esquema)
            observacoes.Add($"o esquema {escolhido} escolhido não se aplica aqui");

        // Ligações da Figura 13 e coluna da Tabela 49 (TN-C-S pela TN-C).
        var coluna = aterramento == EsquemasDeAterramento.TnCS ? EsquemasDeAterramento.TnC : aterramento;
        var ligacaoDaFase = esquema switch
        {
            1 => pen ? LigacoesDoDps.FasePen : LigacoesDoDps.FasePe,
            2 => LigacoesDoDps.FasePe,
            _ => LigacoesDoDps.FaseNeutro
        };
        var ligacoes = new List<(string Ligacao, int Quantidade)> { (ligacaoDaFase, fases) };
        if (esquema != 1) ligacoes.Add((LigacoesDoDps.NeutroPe, 1));
        observacoes.Insert(0, esquema switch
        {
            1 => "DPS entre cada fase e o BEP ou a barra PE",
            2 => "DPS entre cada fase e o PE e entre o neutro e o PE",
            _ => "DPS entre cada fase e o neutro e entre o neutro e o PE"
        });

        foreach (var (ligacao, _) in ligacoes)
        {
            if (!norma.Uc.ContainsKey((ligacao, coluna)))
                problemas.Add($"a Tabela 49 não tem Uc para a ligação {LigacoesDoDps.Descrever(ligacao)} no esquema {coluna}");
        }

        if (problemas.Count > 0) return Falha(quadro, escolha, problemas);

        var uo = quadro.TensaoFaseNeutroV!.Value;
        var monofasica = fases == 1;
        var linhaExterna = escolha.Finalidade is FinalidadeDoDps.LinhaExterna or FinalidadeDoDps.Ambas;
        var descargasDiretas = escolha.Finalidade is FinalidadeDoDps.DescargasDiretas or FinalidadeDoDps.Ambas;
        var passos = new List<PassoDeCalculo>
        {
            new(referencia, "Esquema de conexão dos DPS", expressao, [], esquema, string.Empty, string.Join("; ", observacoes)),
            new(norma.ReferenciaDoEsquemaDeConexao, "Quantidade de DPS", esquema == 1 ? "n = fases" : "n = fases + 1 (neutro–PE)",
                [new ValorDoPasso("fases", fases, string.Empty)], ligacoes.Sum(ligacao => ligacao.Quantidade), "DPS")
        };

        var resultado = new List<LigacaoDoDps>();
        foreach (var (ligacao, _) in ligacoes)
        {
            var uc = norma.Uc[(ligacao, coluna)];
            var tensao = uc.EntreFases ? quadro.TensaoEntreFasesV!.Value : uo;
            var ucMinimo = uc.Fator * tensao;
            string? observacao = null;
            if (ligacao != LigacoesDoDps.NeutroPe)
                observacao = "mínimo da tabela; os valores adequados podem ser bem maiores (nota 4)";
            else if (aterramento == EsquemasDeAterramento.ItComNeutro)
                observacao = "IT: a capacidade de interrupção de corrente subsequente deste DPS é a mesma dos DPS entre fase e neutro (6.3.5.2.4, alínea e)";
            if (coluna != aterramento) observacao = $"coluna {coluna} (PEN aterrado no BEP); {observacao}";
            passos.Add(new PassoDeCalculo($"{norma.ReferenciaDoUc} ({coluna})", $"Uc mínimo — {LigacoesDoDps.Descrever(ligacao)}", uc.Expressao,
                [new ValorDoPasso(uc.Base, tensao, "V")], ucMinimo, "V", observacao));
        }

        var up = esquema == 3 ? "nível global, entre fase e PE (esquema 3); " : string.Empty;
        passos.Add(new PassoDeCalculo($"{norma.ReferenciaDoNivelDeProtecao}; linha {string.Join(", ", linha31!.Sistemas)}", "Nível de proteção máximo (Up)",
            "Up ≤ tabela (Uo), categoria II", [new ValorDoPasso("Uo", uo, "V")], linha31.CategoriaIiKv, "kV",
            $"{up}proteção de modo comum; DPS adicionais para equipamentos entre fase e neutro precisam de nível menor (alínea a, nota 1)"));

        var duasFases = fases == 2
            ? "rede com duas fases tomada como trifásica, o valor maior (critério do Ampere: a norma só distingue as redes trifásicas e as monofásicas)"
            : null;
        decimal? Corrente(CorrenteDoDps corrente, string ligacao, string simbolo, string descricao, string? observacao)
        {
            var neutroPeDoEsquema3 = esquema == 3 && ligacao == LigacoesDoDps.NeutroPe;
            var valor = !neutroPeDoEsquema3 ? corrente.PorModoKa : monofasica ? corrente.NeutroPeMonofasicaKa : corrente.NeutroPeTrifasicaKa;
            var expressaoDaCorrente = !neutroPeDoEsquema3
                ? $"{simbolo} ≥ {N(valor)} kA por modo de proteção"
                : $"{simbolo} ≥ {N(valor)} kA (neutro–PE no esquema 3, rede {(monofasica ? "monofásica" : "trifásica")})";
            var nota = string.Join("; ", new[] { observacao, neutroPeDoEsquema3 ? duasFases : null }.OfType<string>());
            passos.Add(new PassoDeCalculo(corrente.Referencia, $"{descricao} — {LigacoesDoDps.Descrever(ligacao)}", expressaoDaCorrente, [], valor, "kA",
                nota.Length > 0 ? nota : null));
            return valor;
        }

        var correntes = ligacoes.ToDictionary(ligacao => ligacao.Ligacao, _ => (In: (decimal?)null, Iimp: (decimal?)null));
        if (linhaExterna)
        {
            foreach (var (ligacao, _) in ligacoes)
                correntes[ligacao] = correntes[ligacao] with { In = Corrente(norma.CorrenteNominalDeDescarga, ligacao, "In", "Corrente nominal de descarga mínima (In)", null) };
        }

        if (descargasDiretas)
        {
            foreach (var (ligacao, _) in ligacoes)
            {
                correntes[ligacao] = correntes[ligacao] with
                {
                    Iimp = Corrente(norma.CorrenteDeImpulso, ligacao, "Iimp", "Corrente de impulso mínima (Iimp)",
                        "sem a corrente determinada pela IEC 61312-1, que vale quando puder ser determinada")
                };
            }
        }

        if (esquema != 1 && aterramento is EsquemasDeAterramento.TnS or EsquemasDeAterramento.Tt)
        {
            passos.Add(new PassoDeCalculo(norma.ReferenciaDaCorrenteSubsequente, "Corrente subsequente interrompida mínima — neutro–PE",
                $"Ifi ≥ {N(norma.CorrenteSubsequenteMinimaA)} A", [], norma.CorrenteSubsequenteMinimaA, "A", "só para DPS com centelhador"));
        }

        var secao = descargasDiretas ? norma.SecaoDoCondutorDeConexaoDescargasDiretasMm2 : norma.SecaoDoCondutorDeConexaoMm2;
        passos.Add(new PassoDeCalculo(norma.ReferenciaDoCondutorDeConexao, "Seção mínima do condutor de conexão ao PE", $"S ≥ {N(secao)} mm²", [], secao, "mm²",
            descargasDiretas ? "cobre ou equivalente; DPS contra descargas atmosféricas diretas" : "cobre ou equivalente"));
        passos.Add(new PassoDeCalculo(norma.ReferenciaDoCondutorDeConexao, "Comprimento total dos condutores de conexão",
            $"a + b ≤ {N(norma.ComprimentoMaximoDaConexaoM)} m", [], norma.ComprimentoMaximoDaConexaoM, "m",
            "o mais curto possível, sem curvas ou laços; se a + b não puder ficar abaixo disso, o esquema da Figura 15-b"));
        if (escolha.AJusanteDeDr)
        {
            passos.Add(new PassoDeCalculo(norma.ReferenciaDaImunidadeDoDr, "Imunidade mínima do DR a montante a correntes de surto",
                $"I ≥ {N(norma.ImunidadeDoDrKa)} kA", [], norma.ImunidadeDoDrKa, "kA", "onda 8/20 µs; DR instantâneo ou temporizado"));
        }

        foreach (var (ligacao, quantidade) in ligacoes)
        {
            var uc = norma.Uc[(ligacao, coluna)];
            resultado.Add(new LigacaoDoDps(ligacao, quantidade, uc.Fator * (uc.EntreFases ? quadro.TensaoEntreFasesV!.Value : uo),
                correntes[ligacao].In, correntes[ligacao].Iimp));
        }

        return new ResultadoDoDps(quadro, escolha, esquema, resultado, linha31.CategoriaIiKv, secao,
            new MemoriaDeCalculo($"{Nome} {quadro.Nome}", norma.Nome, passos), []);
    }

    /// <summary>Se a Figura 13 deixa o projetista escolher entre os esquemas 2 e 3 (para o diálogo).</summary>
    public static bool EscolheOEsquemaDeConexao(string? esquemaDoQuadro, string esquemaDeAterramento, bool aJusanteDeDr) =>
        esquemaDoQuadro is { } esquema && esquema.EndsWith("+N", StringComparison.Ordinal)
                                       && esquemaDeAterramento is EsquemasDeAterramento.TnS or EsquemasDeAterramento.ItComNeutro
                                           or EsquemasDeAterramento.Tt
                                       && !(esquemaDeAterramento == EsquemasDeAterramento.Tt && !aJusanteDeDr);

    private static ResultadoDoDps Falha(QuadroParaDps quadro, EscolhaDoDps escolha, IReadOnlyList<string> problemas) =>
        new(quadro, escolha, null, [], null, null, null, problemas);

    private static string N(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);
}
