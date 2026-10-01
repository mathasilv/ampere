using Ampere.Core.Alimentadores;
using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Core.Verificacao;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Alimentadores;

/// <summary>
///     Alimentadores contra o perfil FICTÍCIO, com queda total de 7% no ponto de entrega. QD1 3F+N 220 V: IL-01 (600 VA,
///     fase A), TUG-01 (1270 VA, fase B) e TUE-01 (2200 VA, 2F nas fases B e C); demandas 480, 635 e 2200 VA → correntes
///     A 3,78, B 15 e C 10 A; fase-neutro A 3,78, B 5 e C 0 A.
/// </summary>
public class DimensionamentoDeAlimentadores_Teste
{
    private const string Origem = CenarioDeAlimentador.Origem;
    private static readonly CatalogosDeProduto Catalogos = CenarioDeAlimentador.Catalogos;

    [Test]
    public async Task Alimentador_pela_fase_mais_carregada_e_pela_queda_que_sobra()
    {
        var cenario = new CenarioDeAlimentador();

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas).IsEmpty();
        var calculo = resultado.Circuito!.Dimensionamento!;
        await Assert.That(calculo.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
        // Fase B: TUG-01 635 / 127 = 5 A mais o chuveiro 2F 2200 / 220 = 10 A (corrente de linha, não metade de S / V fase-neutro).
        // IB exata, sem passar por S = √3 · V · I (que voltaria 1 ulp acima e recusaria o disjuntor igual a ela).
        await Assert.That(calculo.CorrenteDeProjetoA).IsEqualTo(15m);
        await Assert.That(calculo.SecaoMm2).IsEqualTo(2.5m);
        await Assert.That(calculo.DisjuntorA).IsEqualTo(16m);
        await Assert.That(calculo.IdrSensibilidadeMa).IsNull();
        await Assert.That(calculo.IdrAvaliado).IsTrue();

        var memoria = calculo.Memoria!;
        await Assert.That(memoria.Circuito).IsEqualTo("Alimentador QD1");
        var corrente = Passo(memoria, "Corrente de projeto");
        await Assert.That(corrente.Expressao).IsEqualTo("IB = máx(I(A); I(B); I(C))");
        await Assert.That(corrente.Observacao!).StartsWith("corrente de cada fase do QD1: soma das correntes de linha dos circuitos nela, pela demanda");
        await Assert.That(corrente.Observacao!).Contains("; demanda total 3315 VA; quadro de cargas sha256:");

        // Retorno pelo neutro: IN ≤ 5 − 0 A; √3 · (15 + 5) = 34,6 > 2 · 15 = 30 (entre fases).
        var baseDaQueda = Passo(memoria, "Corrente para a queda de tensão");
        await Assert.That(baseDaQueda.Expressao).IsEqualTo("IΔV = IB + IN; IN = máx(IFN) − mín(IFN)");
        await Assert.That(baseDaQueda.Resultado).IsEqualTo(20m);
        await Assert.That(baseDaQueda.Referencia).IsEqualTo(DimensionamentoDeAlimentadores.CriterioDoAmpere);
        await Assert.That(baseDaQueda.Observacao!).Contains("Premissa: as cargas fase-neutro no mesmo fator de potência");
        var queda = Passo(memoria, "Queda de tensão");
        await Assert.That(queda.Expressao).IsEqualTo("ΔV% = k · ρ · L · IΔV / (S · V) · 100");
        await Assert.That(queda.Valores.Single(valor => valor.Nome == "k").Valor).IsEqualTo(1.7320508075688772935274463415m);
        await Assert.That(queda.Observacao!).StartsWith("fórmula resistiva (sem reatância), só o alimentador");

        var limite = Passo(memoria, "Limite de queda de tensão");
        await Assert.That(limite.Expressao).IsEqualTo("ΔV%máx = ΔV%total − ΔV%terminal");
        await Assert.That(Math.Round(limite.Resultado!.Value, 4)).IsEqualTo(5.7402m);
        await Assert.That(limite.Observacao).IsEqualTo(
            "total: instalação alimentada em baixa tensão pela distribuidora (a partir do ponto de entrega), tomado na origem do alimentador (QGBT): " +
            "o trecho antes dela não está no modelo; terminal: a maior queda dos circuitos do QD1 (TUG-01)");
        await Assert.That(Passo(memoria, "Exigência de IDR").Observacao!).StartsWith("a tabela de IDR por local vale para os circuitos terminais");

        await Assert.That(string.Join("|", cenario.Documento.Chamadas)).IsEqualTo($"transacao:{DimensionamentoDeAlimentadores.NomeDaTransacao}|gravar:900|condicoes:900");
    }

    [Test]
    public async Task Circuito_sem_fase_identificada_soma_na_fase_de_maior_corrente()
    {
        var cenario = new CenarioDeAlimentador(semFaseNoTue: true);

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas).IsEmpty();
        var calculo = resultado.Circuito!.Dimensionamento!;
        // Fases A 3,78, B 5 e C 0 A sem o chuveiro; os 10 A dele podem estar todos na fase B: IB = 15 A, nunca a média.
        await Assert.That(calculo.CorrenteDeProjetoA).IsEqualTo(15m);
        var corrente = Passo(calculo.Memoria!, "Corrente de projeto");
        await Assert.That(corrente.Expressao).IsEqualTo("IB = máx(I(A); I(B); I(C)) + I(sem fase)");
        await Assert.That(corrente.Observacao!).Contains("; sem fase identificada no Revit: TUE-01, somados à fase de maior corrente");
    }

    [Test]
    public async Task Sem_nenhuma_fase_identificada_o_alimentador_trifasico_para()
    {
        var cenario = new CenarioDeAlimentador();
        for (var indice = 0; indice < cenario.Circuitos.Count; indice++) cenario.Circuitos[indice] = cenario.Circuitos[indice] with { FasesNoQuadro = null };

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas.Single()).StartsWith("fases dos circuitos no quadro não identificadas no Revit");
        await Assert.That(resultado.Circuito!.Memoria).IsNull();
    }

    [Test]
    public async Task Carga_fase_neutro_equilibrada_e_carga_entre_fases_queda_com_2_IB()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Adicionar(new CircuitoLido(104, "TUG-02", "TUG", 1270m, "F+N", 127m, ["C"]), CenarioDeAlimentador.Terminal(104, "TUG-02", "TUG", 1270m));

        var calculo = Executar(cenario).Single().Circuito!.Dimensionamento!;

        // Fases A 3,78, B 15 e C 15 A; fase-neutro A 3,78, B 5 e C 5 A: IN ≤ 1,22 A, √3 · 16,22 = 28,1 < 2 · 15 = 30.
        await Assert.That(calculo.CorrenteDeProjetoA).IsEqualTo(15m);
        var baseDaQueda = Passo(calculo.Memoria!, "Corrente para a queda de tensão");
        await Assert.That(baseDaQueda.Expressao).IsEqualTo("IΔV = IB");
        await Assert.That(baseDaQueda.Resultado).IsEqualTo(15m);
        await Assert.That(Passo(calculo.Memoria!, "Queda de tensão").Valores.Single(valor => valor.Nome == "k").Valor).IsEqualTo(2m);
    }

    [Test]
    public async Task So_cargas_trifasicas_queda_equilibrada()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Circuitos.Clear();
        cenario.Terminais.Clear();
        cenario.Adicionar(new CircuitoLido(105, "MOT-01", "Motor", 6600m, "3F", 220m, ["A", "B", "C"]),
            CenarioDeAlimentador.Terminal(105, "MOT-01", "Motor", 6600m, 220m, "3F"));

        var calculo = Executar(cenario).Single().Circuito!.Dimensionamento!;

        await Assert.That(Math.Round(calculo.CorrenteDeProjetoA!.Value, 6)).IsEqualTo(Math.Round(6600m / (1.7320508075688772935274463415m * 220m), 6));
        await Assert.That(calculo.Memoria!.Passos.Any(passo => passo.Descricao == "Corrente para a queda de tensão")).IsFalse();
        await Assert.That(Passo(calculo.Memoria!, "Queda de tensão").Expressao).IsEqualTo("ΔV% = k · ρ · L · IB / (S · V) · 100");
    }

    [Test]
    public async Task Circuito_reserva_fica_fora_da_queda_terminal()
    {
        var cenario = new CenarioDeAlimentador();
        // Reserva sem dados de dimensionamento (nenhum DadosDoCircuito): não pode bloquear o alimentador.
        cenario.Circuitos.Add(new CircuitoLido(106, "RES-01", "Reserva", 0m, "F+N", 127m, ["C"]));

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas).IsEmpty();
        await Assert.That(resultado.Circuito!.Dimensionamento!.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
    }

    [Test]
    public async Task Circuito_fora_do_quadro_de_cargas_impede_o_alimentador()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Circuitos.Add(new CircuitoLido(107, "X-01", null, 900m, "F+N", 127m, ["C"]));

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("quadro de cargas com pendências (circuito X-01: sem AMP_TipoCarga reconhecido");
    }

    [Test]
    public async Task Terminal_com_dimensionamento_desatualizado_no_modelo_impede_o_alimentador()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Documento.MemoriasGravadas[102] = "sha256:de-uma-rodada-anterior";

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).IsEqualTo(
            "circuitos do quadro com o dimensionamento desatualizado no modelo: TUG-01; rode 'Dimensionar circuitos'");
    }

    [Test]
    public async Task Impedimento_do_modelo_apaga_o_alimentador_sem_conferir_o_resto()
    {
        var cenario = new CenarioDeAlimentador { Impedimento = "quadro alimentado por mais de um circuito (A1, A2): o Ampere dimensiona um alimentador por quadro" };
        cenario.Documento.Condicoes = null;

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas.Count).IsEqualTo(2);
        await Assert.That(resultado.Problemas[0]).StartsWith("quadro alimentado por mais de um circuito");
        await Assert.That(resultado.Problemas[1]).StartsWith("o modelo não tem as condições do projeto");
        await Assert.That(resultado.Circuito!.Memoria).IsNull();
        await Assert.That(string.Join("|", cenario.Documento.Chamadas)).IsEqualTo($"transacao:{DimensionamentoDeAlimentadores.NomeDaTransacao}|gravar:900");
    }

    [Test]
    public async Task Quadro_sem_alimentador_no_Revit_nao_grava_nada()
    {
        var cenario = new CenarioDeAlimentador { SemAlimentador = true };

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Circuito).IsNull();
        await Assert.That(resultado.Problemas.Single()).StartsWith("sem circuito alimentador no Revit");
        await Assert.That(cenario.Documento.Chamadas).IsEmpty();
    }

    [Test]
    public async Task Quadro_de_cargas_desatualizado_apaga_o_alimentador_com_o_motivo()
    {
        var cenario = new CenarioDeAlimentador { MemoriaDoQuadro = "sha256:de-uma-montagem-anterior" };

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.Problemas.Single()).StartsWith("quadro de cargas desatualizado");
        await Assert.That(resultado.Circuito!.Memoria).IsNull();
        await Assert.That(string.Join("|", cenario.Documento.Chamadas)).IsEqualTo($"transacao:{DimensionamentoDeAlimentadores.NomeDaTransacao}|gravar:900");
    }

    [Test]
    public async Task Cascata_e_quadro_que_alimenta_quadros_ficam_de_fora()
    {
        var cenario = new CenarioDeAlimentador { OrigemAlimentada = true, AlimentaQuadros = true };

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Count).IsEqualTo(2);
        await Assert.That(problemas[0]).StartsWith("alimentação em cascata (a origem QGBT também é alimentada");
        await Assert.That(problemas[1]).StartsWith("o quadro alimenta outros quadros");
    }

    [Test]
    public async Task Circuito_do_quadro_sem_queda_calculada_impede_o_alimentador()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Terminais[1] = cenario.Terminais[1] with { ComprimentoM = null };

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).IsEqualTo(
            "circuitos do quadro sem queda de tensão ou proteção calculada: TUG-01 (sem AMP_ComprimentoRotaM e sem comprimento do circuito no Revit)");
    }

    [Test]
    public async Task Sem_queda_que_sobre_o_alimentador_para_com_explicacao()
    {
        var cenario = new CenarioDeAlimentador();

        var problemas = DimensionamentoDeAlimentadores.Executar(Origem, CenarioDeAlimentador.ComQuedaTotal(1), Catalogos, cenario.Documento, cenario.Quadros).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("os circuitos do quadro já usam 1,2598% (TUG-01) dos 1% de queda total");
    }

    [Test]
    public async Task Sem_condicoes_do_projeto_pede_o_Dimensionar_antes()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Documento.Condicoes = null;

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("o modelo não tem as condições do projeto");
    }

    [Test]
    public async Task IDR_exigido_pelo_projetista_vale_para_o_alimentador_inteiro()
    {
        var cenario = new CenarioDeAlimentador
        {
            Decisoes = new DecisoesDoProjetista(Idr: DecisoesDoProjetista.ExigirIdr, IdrSensibilidadeMa: 300m, Justificativa: "seletividade com os terminais")
        };

        var calculo = Executar(cenario).Single().Circuito!.Dimensionamento!;

        var exigencia = Passo(calculo.Memoria!, "Exigência de IDR");
        await Assert.That(exigencia.Expressao).IsEqualTo("IDR no alimentador (exigido pelo projetista)");
        await Assert.That(exigencia.Valores).IsEmpty();
        await Assert.That(exigencia.Unidade).IsEqualTo("IDR");
    }

    [Test]
    public async Task Delta_com_neutro_usa_a_tensao_fase_neutro_do_Revit_no_fator_da_queda()
    {
        // 240/120 V (delta com neutro): k = 240 / 120 = 2, não √3 (a queda sobre 138,6 V seria 13% menor).
        var cenario = new CenarioDeAlimentador { Alimentacao = new AlimentacaoDoQuadro("3F+N", 240m, "teste", ["A", "B", "C"], 120m) };
        foreach (var indice in Enumerable.Range(0, cenario.Circuitos.Count))
        {
            var lido = cenario.Circuitos[indice];
            cenario.Circuitos[indice] = lido with { TensaoV = lido.Fases == "F+N" ? 120m : 240m };
            cenario.Terminais[indice] = cenario.Terminais[indice] with
            {
                Pontos = cenario.Terminais[indice].Pontos.Select(ponto => ponto with { TensaoV = lido.Fases == "F+N" ? 120m : 240m }).ToList()
            };
        }

        var calculo = Executar(cenario).Single().Circuito!.Dimensionamento!;

        await Assert.That(Passo(calculo.Memoria!, "Queda de tensão").Valores.Single(valor => valor.Nome == "k").Valor).IsEqualTo(2m);
    }

    [Test]
    public async Task Quadro_trifasico_sem_as_tres_fases_conhecidas_para()
    {
        var cenario = new CenarioDeAlimentador { Alimentacao = new AlimentacaoDoQuadro("3F+N", 220m, "teste") };
        cenario.Circuitos[2] = cenario.Circuitos[2] with { FasesNoQuadro = ["A", "B"] };

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("o quadro trifásico tem 2 fases conhecidas (A, B)");
    }

    [Test]
    public async Task Os_outros_alimentadores_do_quadro_sao_apagados_junto()
    {
        var cenario = new CenarioDeAlimentador
        {
            Impedimento = "quadro alimentado por mais de um circuito (A1, A2): o Ampere dimensiona um alimentador por quadro",
            OutrosAlimentadores = [901, 902]
        };

        var resultado = Executar(cenario).Single();

        await Assert.That(resultado.OutrosApagados!.Select(circuito => circuito.Id)).IsEquivalentTo([901L, 902L]);
        await Assert.That(resultado.OutrosApagados!.All(circuito => circuito.Memoria is null)).IsTrue();
        await Assert.That(string.Join("|", cenario.Documento.Chamadas)).IsEqualTo($"transacao:{DimensionamentoDeAlimentadores.NomeDaTransacao}|gravar:900,901,902");
    }

    [Test]
    public async Task Terminal_parado_no_IDR_impede_o_alimentador()
    {
        var cenario = new CenarioDeAlimentador();
        cenario.Terminais[1] = cenario.Terminais[1] with { Pontos = cenario.Terminais[1].Pontos.Select(ponto => ponto with { Local = null }).ToList() };

        var problemas = Executar(cenario).Single().Problemas;

        await Assert.That(problemas.Single()).StartsWith("circuitos do quadro sem queda de tensão ou proteção calculada: TUG-01");
    }

    [Test]
    public async Task Alimentador_entra_na_lista_de_materiais_com_fases_neutro_e_protecao()
    {
        var cenario = new CenarioDeAlimentador();
        var circuito = Executar(cenario).Single().Circuito!;

        var lista = Ampere.Core.Relatorios.ListaDeMateriais.Montar([circuito]);

        await Assert.That(lista.ForaDaLista).IsEmpty();
        var condutores = lista.Itens.Where(item => item.Grupo == Ampere.Core.Relatorios.ListaDeMateriais.Condutores).ToList();
        // 3F+N, 30 m: 90 m de fase, 30 m de neutro e 30 m de proteção, na seção do alimentador.
        await Assert.That(string.Join("|", condutores.Select(item => $"{item.Quantidade}"))).IsEqualTo("90|30|30");
        await Assert.That(condutores.All(item => item.Circuitos.Single() == "QGBT-Alimentador QD1")).IsTrue();
        await Assert.That(lista.Itens.Single(item => item.Grupo == Ampere.Core.Relatorios.ListaDeMateriais.Disjuntores).Item).IsEqualTo("Disjuntor 3P 16 A");
    }

    [Test]
    public async Task Verificacao_confere_o_alimentador_gravado_com_o_que_o_modelo_da_hoje()
    {
        var cenario = new CenarioDeAlimentador();
        var antes = Verificar(cenario);
        Executar(cenario);
        var emDia = Verificar(cenario);
        cenario.ComprimentoDoAlimentadorM = 45m;
        var depois = Verificar(cenario);

        await Assert.That(Grupos(antes)).IsEqualTo($"Aviso|{VerificacaoDoProjeto.AlimentadoresNaoDimensionados}|900");
        await Assert.That(emDia.Pendencias).IsEmpty();
        await Assert.That(Grupos(depois)).IsEqualTo($"Aviso|{VerificacaoDoProjeto.AlimentadoresDesatualizados}|900");
    }

    [Test]
    public async Task Verificacao_diz_por_que_o_alimentador_nao_tem_calculo()
    {
        var cenario = new CenarioDeAlimentador { OrigemAlimentada = true };

        var pendencia = Verificar(cenario).Pendencias.Single();

        await Assert.That(pendencia.Grupo).IsEqualTo(VerificacaoDoProjeto.AlimentadoresSemCalculo);
        await Assert.That(pendencia.Descricao).StartsWith("alimentador do QD1: alimentação em cascata");
    }

    private static RelatorioDeVerificacao Verificar(CenarioDeAlimentador cenario) =>
        VerificacaoDoProjeto.Executar(new SemPontos(), CenarioDeAlimentador.Perfil, Catalogos, cenario.Quadros, cenario.Documento);

    private static string Grupos(RelatorioDeVerificacao relatorio) =>
        string.Join("\n", relatorio.Pendencias.Select(pendencia => $"{pendencia.Gravidade}|{pendencia.Grupo}|{string.Join(";", pendencia.Elementos)}"));

    // Só os alimentadores: pontos e circuitos terminais ficam com os testes da verificação.
    private sealed class SemPontos : IDocumentoDeVerificacao
    {
        public IReadOnlyList<PontoVerificado> LerPontos() => [];

        public IReadOnlyList<CircuitoVerificado> LerCircuitosDeForca() => [];

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => [];

        public CondicoesDoProjeto? LerCondicoes() => CenarioDeAlimentador.Condicoes;

        public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) => new Dictionary<long, CondicoesDoProjeto>();

        public IReadOnlyDictionary<long, ResultadosNoCircuito> LerResultados(IReadOnlyCollection<long> ids) => new Dictionary<long, ResultadosNoCircuito>();
    }

    private static IReadOnlyList<ResultadoDoAlimentador> Executar(CenarioDeAlimentador cenario) => cenario.Executar();

    private static PassoDeCalculo Passo(MemoriaDeCalculo memoria, string descricao) => memoria.Passos.Single(passo => passo.Descricao == descricao);
}

/// <summary>Cenário dos alimentadores (também usado pelo golden da memória do alimentador).</summary>
internal sealed class CenarioDeAlimentador
{
    public const string Origem = "ponto_de_entrega";
    public static readonly PerfilNormativo Perfil = ComQuedaTotal(7);
    public static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    public static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    public static readonly Dictionary<TipoDeCarga, decimal> Fatores = new() { [TipoDeCarga.TUG] = 0.5m };

    public CenarioDeAlimentador(bool semFaseNoTue = false)
    {
        Terminais =
        [
            Terminal(101, "IL-01", "Iluminação", 600m),
            Terminal(102, "TUG-01", "TUG", 1270m),
            Terminal(103, "TUE-01", "TUE", 2200m, 220m, "2F")
        ];
        Circuitos =
        [
            new CircuitoLido(101, "IL-01", "Iluminação", 600m, "F+N", 127m, ["A"]),
            new CircuitoLido(102, "TUG-01", "TUG", 1270m, "F+N", 127m, ["B"]),
            new CircuitoLido(103, "TUE-01", "TUE", 2200m, "2F", 220m, semFaseNoTue ? null : ["B", "C"])
        ];
    }

    /// <summary>Dados de dimensionamento dos circuitos do quadro.</summary>
    public List<DadosDoCircuito> Terminais { get; }

    /// <summary>Os circuitos do quadro, como o quadro de cargas os lê.</summary>
    public List<CircuitoLido> Circuitos { get; }

    public void Adicionar(CircuitoLido lido, DadosDoCircuito dados)
    {
        Circuitos.Add(lido);
        Terminais.Add(dados);
    }

    public bool SemAlimentador { get; init; }

    public bool OrigemAlimentada { get; init; }

    public bool AlimentaQuadros { get; init; }

    public string? MemoriaDoQuadro { get; init; }

    public string? Impedimento { get; init; }

    public DecisoesDoProjetista? Decisoes { get; init; }

    public decimal ComprimentoDoAlimentadorM { get; set; } = 30m;

    public IReadOnlyList<long>? OutrosAlimentadores { get; init; }

    public AlimentacaoDoQuadro Alimentacao { get; init; } = new("3F+N", 220m, "teste", ["A", "B", "C"]);

    public QuadroLido Quadro => new(1, "QD1", Circuitos.ToList(), Alimentacao);

    public static PerfilNormativo ComQuedaTotal(decimal total) =>
        PerfilNormativo.Carregar(PerfilFicticio.Json.Replace("\"valores\": { \"circuito_terminal\": 5 }",
            $"\"valores\": {{ \"circuito_terminal\": 5, \"ponto_de_entrega\": {total} }}"));

    public IReadOnlyList<ResultadoDoAlimentador> Executar() => DimensionamentoDeAlimentadores.Executar(Origem, Perfil, Catalogos, Documento, Quadros);

    public DocumentoFalso Documento => _documento ??= new DocumentoFalso(this);

    public QuadrosFalsos Quadros => _quadros ??= new QuadrosFalsos(this);

    private DocumentoFalso? _documento;
    private QuadrosFalsos? _quadros;

    public static DadosDoCircuito Terminal(long id, string numero, string tipo, decimal potenciaVA, decimal tensaoV = 127m, string fases = "F+N") =>
        new(id, numero, tipo, 10m, null, null, [new DadosDoPonto(id * 10, potenciaVA, tensaoV, fases, "LOCAL-SECO", tipo)], Quadro: "QD1");

    public sealed class DocumentoFalso(CenarioDeAlimentador cenario) : IDocumentoDeAlimentadores
    {
        public List<string> Chamadas { get; } = [];

        public CondicoesDoProjeto? Condicoes { get; set; } = CenarioDeAlimentador.Condicoes;

        /// <summary>AMP_MemoriaCalculoId gravado por circuito; sem entrada, o da rodada em dia (o recalculado).</summary>
        public Dictionary<long, string> MemoriasGravadas { get; } = [];

        public void EmUmaTransacao(string nome, Action acao)
        {
            Chamadas.Add($"transacao:{nome}");
            acao();
        }

        public IReadOnlyList<QuadroComAlimentador> LerAlimentadores() =>
        [
            new(1, "QD1",
                cenario.SemAlimentador ? null : new DadosDoAlimentador(900, cenario.ComprimentoDoAlimentadorM, null, null, null, cenario.Decisoes),
                "QGBT", cenario.OrigemAlimentada, cenario.AlimentaQuadros, cenario.Impedimento, cenario.OutrosAlimentadores)
        ];

        public CondicoesDoProjeto? LerCondicoes() => Condicoes;

        public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) => new Dictionary<long, CondicoesDoProjeto>();

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => cenario.Terminais.Where(circuito => ids.Contains(circuito.Id)).ToList();

        public IReadOnlyDictionary<long, ResultadosNoCircuito> LerResultados(IReadOnlyCollection<long> ids) =>
            LerCircuitos(ids).ToDictionary(dados => dados.Id, dados =>
                {
                    var emDia = ResultadosNoCircuito.De(DimensionamentoDoProjeto.Calcular(dados, Condicoes ?? CenarioDeAlimentador.Condicoes, Perfil, Catalogos));
                    return MemoriasGravadas.TryGetValue(dados.Id, out var gravada) ? emDia with { MemoriaCalculoId = gravada } : emDia;
                })
                .Concat(Gravados.Where(par => ids.Contains(par.Key)))
                .ToDictionary(par => par.Key, par => par.Value);

        /// <summary>Os AMP_* de resultado que os alimentadores gravaram.</summary>
        public Dictionary<long, ResultadosNoCircuito> Gravados { get; } = [];

        public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados)
        {
            Chamadas.Add($"gravar:{string.Join(",", resultados.Select(resultado => resultado.Id))}");
            foreach (var resultado in resultados) Gravados[resultado.Id] = ResultadosNoCircuito.De(resultado);
        }

        public void GravarCondicoes(CondicoesDoProjeto condicoes, IReadOnlyCollection<long> circuitos, bool doProjetoTodo)
        {
            if (doProjetoTodo) throw new InvalidOperationException("alimentador não troca as condições do projeto");
            Chamadas.Add($"condicoes:{string.Join(",", circuitos)}");
        }
    }

    public sealed class QuadrosFalsos(CenarioDeAlimentador cenario) : IDocumentoDeQuadros
    {
        public void EmUmaTransacao(string nome, Action acao) => acao();

        public IReadOnlyList<QuadroLido> LerQuadrosComCircuitos() => [cenario.Quadro];

        public string? LerMemoriaDoQuadro(long quadroId) =>
            cenario.MemoriaDoQuadro ?? QuadroDeCargasDoProjeto.Montar(cenario.Quadro, Perfil, Fatores).Quadro.Memoria!.Hash();

        public IReadOnlyDictionary<TipoDeCarga, decimal>? LerFatoresDoQuadro(long quadroId) => Fatores;

        public void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas) => throw new NotSupportedException();

        public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados) =>
            throw new NotSupportedException();

        public IReadOnlyList<string> ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados) => throw new NotSupportedException();

        public IReadOnlyList<CircuitoLido> LerCircuitosSemQuadro() => throw new NotSupportedException();

        public string CriarTabelaDoQuadro(string nomeDoQuadro) => throw new NotSupportedException();
    }
}
