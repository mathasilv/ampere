using Ampere.Core.Cargas;
using Ampere.Core.Memoria;

namespace Ampere.Core.Demanda;

/// <summary>Ponto de carga do modelo, como o adapter o lê (nulo = parâmetro vazio).</summary>
/// <param name="TipoDeCarga">AMP_TipoCarga.</param>
/// <param name="Aparelho">AMP_Aparelho (só conta em TUE).</param>
/// <param name="PotenciaVA">AMP_PotenciaInstaladaVA.</param>
public sealed record PontoDeDemanda(long Id, string? TipoDeCarga, string? Aparelho, decimal? PotenciaVA);

/// <summary>Como os motores entram na demanda: a regra do documento (ex.: d.1) ou o fator do projetista (ex.: d.2).</summary>
public enum RegraDeMotores
{
    /// <summary>Regra do documento para edifícios residenciais de uso coletivo (ex.: 80% no maior, 50% nos demais).</summary>
    DoDocumento,

    /// <summary>Fator informado pelo projetista, com a justificativa (indústrias e outros).</summary>
    DoProjetista
}

/// <summary>O que o projetista escolhe para a demanda.</summary>
/// <param name="Edificacao">Linha da tabela de iluminação e tomadas (ex.: Residências).</param>
/// <param name="Motores">Exigida quando há motores.</param>
/// <param name="FatorDosMotoresPct">Com <see cref="RegraDeMotores.DoProjetista" />: fator em (0; 100].</param>
/// <param name="Justificativa">Com <see cref="RegraDeMotores.DoProjetista" />: por que esse fator.</param>
public sealed record OpcoesDaDemanda(string Edificacao, RegraDeMotores? Motores = null, decimal? FatorDosMotoresPct = null, string? Justificativa = null);

/// <summary>Uma parcela da demanda (a, b1, …), com os pontos que entram nela.</summary>
public sealed record ParcelaDaDemanda(string Codigo, string Descricao, int Pontos, decimal PotenciaInstaladaVA, decimal DemandaVA);

/// <summary>Demanda da entrada: o total (nulo se não saiu), as parcelas, a memória e o que impediu.</summary>
/// <param name="PontosComProblema">Pontos que impediram o cálculo (para selecionar no modelo).</param>
public sealed record ResultadoDaDemanda(
    decimal? DemandaVA,
    decimal PotenciaInstaladaVA,
    IReadOnlyList<ParcelaDaDemanda> Parcelas,
    MemoriaDeCalculo? Memoria,
    IReadOnlyList<string> Problemas,
    IReadOnlyList<long> PontosComProblema);

/// <summary>Porta da demanda (implementada pelo adapter Revit): os pontos de carga do modelo.</summary>
public interface IDocumentoDeDemanda
{
    /// <summary>Os pontos de carga (com conector de força) do documento, classificados ou não.</summary>
    IReadOnlyList<PontoDeDemanda> LerPontos();
}

/// <summary>
///     Caso de uso "Demanda da entrada": a demanda da instalação pela norma da distribuidora (ex.: CELG CT 04/18, item 10:
///     D = a + (b1 + … + b8) + c + d + e), com memória de cálculo. Não grava nada no modelo.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>a: iluminação e TUG, pela linha da edificação; P em kW tomada igual à potência aparente instalada, em kVA
///         (decisão do usuário, 01/10/2026). A carga mínima por área não é conferida (o Ampere não lê a área).</item>
///         <item>b: TUE pelo aparelho (AMP_Aparelho), cada tipo com a sua quantidade; fornos e fogões em duas faixas de
///         potência, cada uma com a sua quantidade. TUE sem aparelho para o cálculo.</item>
///         <item>c: ar-condicionado pela quantidade (coluna residencial nas residências, comercial nas demais).</item>
///         <item>d: motores pela regra do documento ou pelo fator do projetista (escolha exigida quando há motores).</item>
///         <item>e: máquinas de solda a transformador, por ordem de potência.</item>
///         <item>f: aparelho "Outro" (fora das tabelas) entra sem fator — critério do Ampere, a favor da segurança.</item>
///         <item>Reservas não entram (como manda o documento). Ponto sem tipo ou sem potência para o cálculo.</item>
///     </list>
/// </remarks>
public static class DemandaDaEntrada
{
    public const string Nome = "Demanda da entrada";

    /// <summary>Referência da parcela de aparelhos fora das tabelas: critério do Ampere.</summary>
    public const string CriterioDosOutros = "Critério do Ampere, não item do documento: aparelho fora das tabelas entra sem fator (a favor da segurança)";

    private const string DecisaoDaPotencia = "potência instalada em kVA tomada como kW (decisão do usuário, 01/10/2026)";

    private static readonly (string Codigo, Aparelho Aparelho, string Descricao)[] ParcelasDosAparelhos =
    [
        ("b1", Aparelho.Chuveiro, "chuveiros"),
        ("b2", Aparelho.Torneira, "torneiras"),
        ("b3", Aparelho.LavaLoucas, "lava-louças"),
        ("b4", Aparelho.AquecedorDePassagem, "aquecedores de passagem"),
        ("b5", Aparelho.AquecedorDeAcumulacao, "aquecedores de acumulação"),
        ("b7", Aparelho.SecadoraDeRoupa, "secadoras de roupa"),
        ("b8", Aparelho.MicroOndas, "micro-ondas")
    ];

    public static ResultadoDaDemanda Executar(IDocumentoDeDemanda documento, OpcoesDaDemanda opcoes, PerfilDeDemanda perfil) =>
        Calcular(documento.LerPontos(), opcoes, perfil);

    public static ResultadoDaDemanda Calcular(IReadOnlyList<PontoDeDemanda> pontos, OpcoesDaDemanda opcoes, PerfilDeDemanda perfil)
    {
        var problemas = new List<string>();
        var comProblema = new List<long>();
        var edificacao = perfil.Edificacoes.FirstOrDefault(linha => linha.Nome == opcoes.Edificacao?.Trim());
        if (edificacao is null)
            problemas.Add($"edificação '{opcoes.Edificacao}' fora da tabela de iluminação e tomadas ({perfil.ReferenciaDaIluminacao})");

        var lidos = Ler(pontos, problemas, comProblema);
        var motores = lidos.Where(ponto => ponto.Tipo == TipoDeCarga.Motor).ToList();
        if (motores.Count > 0) Motores(opcoes, motores.Count, perfil, problemas);
        if (lidos.Count == 0 && problemas.Count == 0) problemas.Add("nenhum ponto classificado com potência: nada a somar");

        var instalada = lidos.Sum(ponto => ponto.PotenciaVA);
        if (problemas.Count > 0 || edificacao is null) return new ResultadoDaDemanda(null, instalada, [], null, problemas, comProblema);

        var calculo = new Calculo(perfil, edificacao, opcoes);
        calculo.Iluminacao(lidos.Where(ponto => ponto.Tipo is TipoDeCarga.Iluminacao or TipoDeCarga.TUG).ToList());
        foreach (var (codigo, aparelho, descricao) in ParcelasDosAparelhos.Where(parcela => string.CompareOrdinal(parcela.Codigo, "b6") < 0))
            calculo.Aparelhos(codigo, descricao, aparelho, lidos.Where(ponto => ponto.Aparelho == aparelho).ToList());
        calculo.Fornos(lidos.Where(ponto => ponto.Aparelho == Aparelho.FornoOuFogao).ToList());
        foreach (var (codigo, aparelho, descricao) in ParcelasDosAparelhos.Where(parcela => string.CompareOrdinal(parcela.Codigo, "b6") > 0))
            calculo.Aparelhos(codigo, descricao, aparelho, lidos.Where(ponto => ponto.Aparelho == aparelho).ToList());
        calculo.ArCondicionado(lidos.Where(ponto => ponto.Tipo == TipoDeCarga.ArCondicionado).ToList());
        calculo.Motores(motores);
        calculo.MaquinasDeSolda(lidos.Where(ponto => ponto.Aparelho == Aparelho.MaquinaDeSolda).ToList());
        calculo.Outros(lidos.Where(ponto => ponto.Aparelho == Aparelho.Outro).ToList());
        return calculo.Total(instalada);
    }

    private sealed record Lido(long Id, TipoDeCarga Tipo, Aparelho? Aparelho, decimal PotenciaVA);

    // Os pontos que entram (reservas fora); sem tipo, sem potência ou TUE sem aparelho viram problema, com os ids.
    private static List<Lido> Ler(IReadOnlyList<PontoDeDemanda> pontos, List<string> problemas, List<long> comProblema)
    {
        var semTipo = new List<long>();
        var semPotencia = new List<long>();
        var semAparelho = new List<long>();
        var lidos = new List<Lido>();
        foreach (var ponto in pontos.OrderBy(ponto => ponto.Id))
        {
            if (!CodigosDeTipoDeCarga.TryLer(ponto.TipoDeCarga, out var tipo))
            {
                semTipo.Add(ponto.Id);
                continue;
            }

            if (tipo == TipoDeCarga.Reserva) continue;
            if (ponto.PotenciaVA is not { } potencia || potencia < 0m)
            {
                semPotencia.Add(ponto.Id);
                continue;
            }

            Aparelho? aparelho = null;
            if (tipo == TipoDeCarga.TUE)
            {
                if (!CodigosDeAparelho.TryLer(ponto.Aparelho, out var lido))
                {
                    semAparelho.Add(ponto.Id);
                    continue;
                }

                aparelho = lido;
            }

            lidos.Add(new Lido(ponto.Id, tipo, aparelho, potencia));
        }

        Problema(semTipo, "ponto(s) sem AMP_TipoCarga: classifique-os (ou apague-os, se não são carga)", problemas, comProblema);
        Problema(semPotencia, "ponto(s) sem AMP_PotenciaInstaladaVA", problemas, comProblema);
        Problema(semAparelho, "ponto(s) TUE sem AMP_Aparelho: a distribuidora dá um fator para cada tipo de aparelho (chuveiro, torneira, forno ou fogão…); use 'Outro' para os que não estão nas tabelas", problemas, comProblema);
        return lidos;
    }

    private static void Problema(List<long> ids, string descricao, List<string> problemas, List<long> comProblema)
    {
        if (ids.Count == 0) return;
        problemas.Add($"{ids.Count} {descricao}");
        comProblema.AddRange(ids);
    }

    private static void Motores(OpcoesDaDemanda opcoes, int quantos, PerfilDeDemanda perfil, List<string> problemas)
    {
        switch (opcoes.Motores)
        {
            case null:
                problemas.Add($"{quantos} motor(es): escolha a regra — a do documento ({perfil.Motores.Aplicacao}, {perfil.Motores.Referencia}) ou o fator do projetista ({perfil.Motores.ReferenciaDoProjetista})");
                break;
            case RegraDeMotores.DoProjetista when opcoes.FatorDosMotoresPct is not (> 0m and <= 100m) || string.IsNullOrWhiteSpace(opcoes.Justificativa):
                problemas.Add("fator dos motores do projetista: informe o fator em (0; 100]% e a justificativa");
                break;
        }
    }

    private sealed class Calculo(PerfilDeDemanda perfil, EdificacaoDeDemanda edificacao, OpcoesDaDemanda opcoes)
    {
        private readonly List<PassoDeCalculo> _passos = [];
        private readonly List<ParcelaDaDemanda> _parcelas = [];

        public void Iluminacao(List<Lido> pontos)
        {
            var potencia = Kva(pontos.Sum(ponto => ponto.PotenciaVA));
            var observacao = $"{edificacao.Nome}; P: {Contagem(pontos.Count)} de iluminação e TUG, {DecisaoDaPotencia}; " +
                             $"a carga mínima de {NumeroEmTexto.Formatar(edificacao.CargaMinimaWm2)} W/m² não é conferida (o Ampere não lê a área)";
            decimal demanda;
            if (edificacao.FatorPct is { } unico)
            {
                demanda = potencia * unico / 100m;
                Passo(perfil.ReferenciaDaIluminacao, "a — iluminação e TUG", "a = P · fd", [Valor("P", potencia, "kVA"), Valor("fd", unico / 100m, string.Empty)], demanda, observacao);
            }
            else if (edificacao.PorFaixa is { } faixas)
            {
                var faixa = new TabelaDeFaixas(perfil.ReferenciaDaIluminacao, faixas).Para(potencia);
                demanda = potencia * faixa.FatorPct / 100m;
                Passo(perfil.ReferenciaDaIluminacao, "a — iluminação e TUG", "a = P · fd(P)", [Valor("P", potencia, "kVA"), Valor("fd", faixa.FatorPct / 100m, string.Empty)],
                    demanda, $"{observacao}; faixa {Faixa(faixas, faixa, "P", "kW")}: o fator vale para P inteira, como impresso");
            }
            else
            {
                var (expressao, valor) = Escalonado(edificacao.Escalonado!, potencia);
                demanda = valor;
                Passo(perfil.ReferenciaDaIluminacao, "a — iluminação e TUG", expressao, [Valor("P", potencia, "kVA")], demanda, observacao);
            }

            _parcelas.Add(new ParcelaDaDemanda("a", "iluminação e TUG", pontos.Count, pontos.Sum(ponto => ponto.PotenciaVA), demanda * 1000m));
        }

        public void Aparelhos(string codigo, string descricao, Aparelho aparelho, List<Lido> pontos)
        {
            if (pontos.Count == 0) return;
            var coluna = perfil.ColunaDe(aparelho)!;
            var faixa = new TabelaDeFaixas(perfil.ReferenciaDosAparelhos, coluna.Faixas).Para(pontos.Count);
            PorQuantidade(codigo, descricao, perfil.ReferenciaDosAparelhos, pontos, faixa,
                $"coluna \"{coluna.Nome}\"; faixa {Faixa(coluna.Faixas, faixa, "n", string.Empty)}; P: {DecisaoDaPotencia}");
        }

        // Duas faixas de potência, cada uma com a sua quantidade (as colunas da tabela): b6a até o limite, b6b acima.
        public void Fornos(List<Lido> pontos)
        {
            var limiteVA = perfil.LimiteDosFornosKw * 1000m;
            var limite = NumeroEmTexto.Formatar(perfil.LimiteDosFornosKw);
            var ate = pontos.Where(ponto => ponto.PotenciaVA <= limiteVA).ToList();
            var acima = pontos.Where(ponto => ponto.PotenciaVA > limiteVA).ToList();
            if (ate.Count > 0)
            {
                var faixa = perfil.FornosAteOLimite.Para(ate.Count);
                PorQuantidade("b6a", $"fornos e fogões até {limite} kW", perfil.FornosAteOLimite.Referencia, ate, faixa,
                    $"coluna \"até {limite} kW\" (cada aparelho pela sua potência); faixa {Faixa(perfil.FornosAteOLimite.Faixas, faixa, "n", string.Empty)}; P: {DecisaoDaPotencia}");
            }

            if (acima.Count > 0)
            {
                var faixa = perfil.FornosAcimaDoLimite.Para(acima.Count);
                PorQuantidade("b6b", $"fornos e fogões acima de {limite} kW", perfil.FornosAcimaDoLimite.Referencia, acima, faixa,
                    $"coluna \"superior a {limite} kW\" (cada aparelho pela sua potência); faixa {Faixa(perfil.FornosAcimaDoLimite.Faixas, faixa, "n", string.Empty)}; P: {DecisaoDaPotencia}");
            }
        }

        public void ArCondicionado(List<Lido> pontos)
        {
            if (pontos.Count == 0) return;
            var tabela = edificacao.Residencial ? perfil.ArCondicionadoResidencial : perfil.ArCondicionadoComercial;
            var faixa = tabela.Para(pontos.Count);
            PorQuantidade("c", "ar-condicionado", tabela.Referencia, pontos, faixa,
                $"coluna {(edificacao.Residencial ? "residencial" : "comercial")} ({edificacao.Nome}); faixa {Faixa(tabela.Faixas, faixa, "n", string.Empty)}; " +
                "unidade central de condicionamento entra com 100% (classifique-a como TUE, aparelho Outro)");
        }

        public void Motores(List<Lido> pontos)
        {
            if (pontos.Count == 0) return;
            var ordenados = pontos.OrderByDescending(ponto => ponto.PotenciaVA).ThenBy(ponto => ponto.Id).ToList();
            decimal demanda;
            if (opcoes.Motores == RegraDeMotores.DoDocumento)
            {
                var maior = Kva(ordenados[0].PotenciaVA);
                var demais = Kva(ordenados.Skip(1).Sum(ponto => ponto.PotenciaVA));
                var regra = perfil.Motores;
                demanda = maior * regra.MaiorPct / 100m + demais * regra.DemaisPct / 100m;
                Passo(regra.Referencia, "d — motores", $"d = {Fator(regra.MaiorPct)} · Pmaior + {Fator(regra.DemaisPct)} · Pdemais",
                    [Valor("Pmaior", maior, "kVA"), Valor("Pdemais", demais, "kVA")], demanda,
                    $"regra do documento para {regra.Aplicacao}, escolhida pelo projetista; {Contagem(pontos.Count)}");
            }
            else
            {
                var potencia = Kva(pontos.Sum(ponto => ponto.PotenciaVA));
                var fator = opcoes.FatorDosMotoresPct!.Value / 100m;
                demanda = potencia * fator;
                Passo(perfil.Motores.ReferenciaDoProjetista, "d — motores", "d = fd · P", [Valor("P", potencia, "kVA"), Valor("fd", fator, string.Empty)], demanda,
                    $"fator do projetista ({opcoes.Justificativa!.Trim()}); {Contagem(pontos.Count)}");
            }

            _parcelas.Add(new ParcelaDaDemanda("d", "motores", pontos.Count, pontos.Sum(ponto => ponto.PotenciaVA), demanda * 1000m));
        }

        public void MaquinasDeSolda(List<Lido> pontos)
        {
            if (pontos.Count == 0) return;
            var regra = perfil.MaquinasDeSolda;
            var ordenados = pontos.OrderByDescending(ponto => ponto.PotenciaVA).ThenBy(ponto => ponto.Id).ToList();
            var termos = new List<string>();
            var valores = new List<ValorDoPasso>();
            var demanda = 0m;
            for (var indice = 0; indice < Math.Min(ordenados.Count, regra.PorOrdemPct.Count); indice++)
            {
                var potencia = Kva(ordenados[indice].PotenciaVA);
                termos.Add($"{Fator(regra.PorOrdemPct[indice])} · P{indice + 1}");
                valores.Add(Valor($"P{indice + 1}", potencia, "kVA"));
                demanda += potencia * regra.PorOrdemPct[indice] / 100m;
            }

            if (ordenados.Count > regra.PorOrdemPct.Count)
            {
                var demais = Kva(ordenados.Skip(regra.PorOrdemPct.Count).Sum(ponto => ponto.PotenciaVA));
                termos.Add($"{Fator(regra.DemaisPct)} · Pdemais");
                valores.Add(Valor("Pdemais", demais, "kVA"));
                demanda += demais * regra.DemaisPct / 100m;
            }

            Passo(regra.Referencia, "e — máquinas de solda", $"e = {string.Join(" + ", termos)}", valores, demanda,
                $"máquinas de solda a transformador, da maior para a menor potência; {Contagem(pontos.Count)}");
            _parcelas.Add(new ParcelaDaDemanda("e", "máquinas de solda", pontos.Count, pontos.Sum(ponto => ponto.PotenciaVA), demanda * 1000m));
        }

        public void Outros(List<Lido> pontos)
        {
            if (pontos.Count == 0) return;
            var potencia = Kva(pontos.Sum(ponto => ponto.PotenciaVA));
            Passo(CriterioDosOutros, "f — outros aparelhos", "f = P", [Valor("P", potencia, "kVA")], potencia,
                $"aparelho 'Outro' (fora das tabelas do documento); {Contagem(pontos.Count)}");
            _parcelas.Add(new ParcelaDaDemanda("f", "outros aparelhos", pontos.Count, pontos.Sum(ponto => ponto.PotenciaVA), potencia * 1000m));
        }

        public ResultadoDaDemanda Total(decimal instaladaVA)
        {
            var total = _parcelas.Sum(parcela => parcela.DemandaVA) / 1000m;
            Passo(perfil.ReferenciaDaFormula, "Demanda total", $"D = {string.Join(" + ", _parcelas.Select(parcela => parcela.Codigo))}",
                _parcelas.Select(parcela => Valor(parcela.Codigo, parcela.DemandaVA / 1000m, "kVA")).ToList(), total,
                $"fórmula do documento: {perfil.Expressao}; parcelas sem pontos ficam de fora; reservas não entram");
            var memoria = new MemoriaDeCalculo(Nome, perfil.Nome, _passos);
            return new ResultadoDaDemanda(total * 1000m, instaladaVA, _parcelas, memoria, [], []);
        }

        private void PorQuantidade(string codigo, string descricao, string referencia, List<Lido> pontos, FaixaDeDemanda faixa, string observacao)
        {
            var potencia = Kva(pontos.Sum(ponto => ponto.PotenciaVA));
            var fator = faixa.FatorPct / 100m;
            var demanda = potencia * fator;
            Passo(referencia, $"{codigo} — {descricao}", $"{codigo} = fd(n) · P",
                [Valor("n", pontos.Count, "aparelhos"), Valor("P", potencia, "kVA"), Valor("fd", fator, string.Empty)], demanda, observacao);
            _parcelas.Add(new ParcelaDaDemanda(codigo, descricao, pontos.Count, pontos.Sum(ponto => ponto.PotenciaVA), demanda * 1000m));
        }

        // Escalonado: cada faixa leva a parcela de P até o seu limite (acumulado), com o seu fator.
        private static (string Expressao, decimal Demanda) Escalonado(IReadOnlyList<FaixaDeDemanda> faixas, decimal potencia)
        {
            var termos = new List<string>();
            var demanda = 0m;
            var anterior = 0m;
            foreach (var faixa in faixas)
            {
                if (potencia <= anterior) break;
                var ate = faixa.Ate is { } limite ? Math.Min(potencia, limite) : potencia;
                var parcela = ate - anterior;
                termos.Add(faixa.Ate is { } fim && potencia > fim
                    ? $"{NumeroEmTexto.Formatar(fim - anterior)} · {Fator(faixa.FatorPct)}"
                    : anterior == 0m ? $"P · {Fator(faixa.FatorPct)}" : $"(P − {NumeroEmTexto.Formatar(anterior)}) · {Fator(faixa.FatorPct)}");
                demanda += parcela * faixa.FatorPct / 100m;
                anterior = faixa.Ate ?? potencia;
            }

            return ($"a = {(termos.Count == 0 ? "0" : string.Join(" + ", termos))}", demanda);
        }

        private void Passo(string referencia, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, decimal resultado, string observacao) =>
            _passos.Add(new PassoDeCalculo(referencia, descricao, expressao, valores, resultado, "kVA", observacao));
    }

    private static ValorDoPasso Valor(string nome, decimal valor, string unidade) => new(nome, valor, unidade);

    private static decimal Kva(decimal va) => va / 1000m;

    private static string Fator(decimal pct) => NumeroEmTexto.Formatar(pct / 100m);

    private static string Contagem(int quantos) => quantos == 1 ? "1 ponto" : $"{quantos} pontos";

    // A faixa em texto, como a tabela: "5 < P ≤ 6 kW", "n ≤ 1", "n > 65".
    private static string Faixa(IReadOnlyList<FaixaDeDemanda> faixas, FaixaDeDemanda faixa, string variavel, string unidade)
    {
        var indice = faixas.ToList().IndexOf(faixa);
        var anterior = indice > 0 ? faixas[indice - 1].Ate : null;
        var sufixo = unidade.Length > 0 ? $" {unidade}" : string.Empty;
        return (anterior, faixa.Ate) switch
        {
            (null, { } ate) => $"{variavel} ≤ {NumeroEmTexto.Formatar(ate)}{sufixo}",
            ({ } de, null) => $"{variavel} > {NumeroEmTexto.Formatar(de)}{sufixo}",
            ({ } de, { } ate) => $"{NumeroEmTexto.Formatar(de)} < {variavel} ≤ {NumeroEmTexto.Formatar(ate)}{sufixo}",
            _ => "única"
        } + $": {NumeroEmTexto.Formatar(faixa.FatorPct)}%";
    }
}
