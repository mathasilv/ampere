using Ampere.Core;
using Ampere.Core.Cargas;
using Ampere.Core.Previsao;

namespace Ampere.Tests.Core.Previsao;

[Property("Fonte", "NBR 5410:2004, item 9.5.2")]
public class NormaDePrevisao_Teste
{
    private static readonly NormaDePrevisao Norma = NormaDePrevisao.NBR5410_2004;

    [Test]
    [Arguments(2, 100)]
    [Arguments(6, 100)]
    [Arguments(9.99, 100)]
    [Arguments(10, 160)]
    [Arguments(13.99, 160)]
    [Arguments(14, 220)]
    [Arguments(22, 340)]
    public async Task Iluminacao_minima_pela_area(decimal areaM2, decimal minimaVA)
    {
        await Assert.That(Norma.IluminacaoMinimaVA(areaM2)).IsEqualTo(minimaVA);
    }

    [Test]
    [Arguments("Banheiro", 3, 7, 1)]
    [Arguments("Cozinha, copa, área de serviço ou lavanderia", 9, 7, 2)]
    [Arguments("Cozinha, copa, área de serviço ou lavanderia", 9, 7.001, 3)]
    [Arguments("Cozinha, copa, área de serviço ou lavanderia", 6, 10, 3)]
    [Arguments("Varanda", 12, 14, 1)]
    [Arguments("Sala ou dormitório", 9, 12, 3)]
    [Arguments("Sala ou dormitório", 4, 8, 2)]
    [Arguments("Demais cômodos", 2, 6, 1)]
    [Arguments("Demais cômodos", 6, 10, 1)]
    [Arguments("Demais cômodos", 6.5, 10.2, 3)]
    public async Task Pontos_de_tomada_minimos_por_categoria(string categoria, decimal areaM2, decimal perimetroM, int minimos)
    {
        await Assert.That(Norma.Regra(categoria)!.PontosMinimos(areaM2, perimetroM)).IsEqualTo(minimos);
    }

    [Test]
    public async Task Categorias_na_ordem_do_arquivo_com_a_de_fora_da_habitacao_no_fim()
    {
        await Assert.That(string.Join("|", PrevisaoDeCargas.Categorias(Norma))).IsEqualTo(
            "Banheiro|Cozinha, copa, área de serviço ou lavanderia|Varanda|Sala ou dormitório|Demais cômodos|Não é local de habitação");
        await Assert.That(Norma.ReferenciaDaIluminacao).StartsWith("NBR 5410:2004, itens 9.5.2.1.1");
        await Assert.That(Norma.Regra("Demais cômodos")!.Descrever()).IsEqualTo("1 ponto até 6 m²; acima, um ponto a cada 5 m de perímetro, ou fração");
    }

    [Test]
    public async Task Arquivo_sem_ref_ou_real_com_ref_ficticia_e_recusado()
    {
        var json = Recurso();

        var semRef = System.Text.RegularExpressions.Regex.Replace(json, "\"ref\": \"NBR 5410:2004, itens 9\\.5\\.2\\.1\\.1[^\"]*\"", "\"ref\": \"TODO_NORMA\"");

        await Assert.That(semRef).Contains("\"ref\": \"TODO_NORMA\"");
        await Assert.That(() => NormaDePrevisao.Carregar(semRef)).Throws<PrevisaoInvalidaException>().WithMessageContaining("iluminacao: sem ref (valor sem fonte)");
        await Assert.That(() => NormaDePrevisao.Carregar(json.Replace("\"ref\": \"NBR 5410:2004, item 9.5.2.2.1", "\"ref\": \"FICTÍCIO 9.5.2.2.1")))
            .Throws<PrevisaoInvalidaException>().WithMessageContaining("arquivo real com referência fictícia");
        await Assert.That(() => NormaDePrevisao.Carregar(json.Replace("\"perimetro_por_ponto_m\": 3.5,", "\"perimetro_por_ponto_m\": 0,")))
            .Throws<PrevisaoInvalidaException>().WithMessageContaining("perimetro_por_ponto_m: ausente ou não positivo");
    }

    private static string Recurso()
    {
        using var fluxo = typeof(NormaDePrevisao).Assembly.GetManifestResourceStream("Ampere.Core.Previsao.NBR5410_2004.previsao_de_cargas.json")!;
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}

[Property("Fonte", "NBR 5410:2004, item 9.5.2")]
public class PrevisaoDeCargas_Teste
{
    private const string Cozinha = "Cozinha, copa, área de serviço ou lavanderia";
    private static readonly NormaDePrevisao Norma = NormaDePrevisao.NBR5410_2004;

    [Test]
    public async Task Cozinha_com_tres_de_600_e_iluminacao_pela_area_atende()
    {
        // 10 m²: 160 VA de iluminação; 12 m de perímetro: 4 pontos (a cada 3,5 m ou fração).
        var cozinha = Comodo("Cozinha", 10m, 12m, Luz(160m), Tug(600m), Tug(600m), Tug(600m), Tug(100m));

        var avaliacao = Avaliar([cozinha], ("Cozinha", Cozinha)).Comodos.Single();

        await Assert.That(avaliacao.Situacao).IsEqualTo(SituacaoDoComodo.Atende);
        await Assert.That(avaliacao.IluminacaoMinimaVA).IsEqualTo(160m);
        await Assert.That(avaliacao.TomadasMinimas).IsEqualTo(4);
        await Assert.That(avaliacao.Faltas).IsEmpty();
        await Assert.That(avaliacao.Observacoes.Single()).StartsWith("acima da bancada da pia");
    }

    [Test]
    public async Task Poucas_de_600_nao_atendem_com_o_conjunto_pequeno_e_atendem_pela_alternativa_com_o_conjunto_grande()
    {
        var cozinha = Comodo("Cozinha", 9m, 10m, Luz(100m), Tug(600m), Tug(600m), Tug(100m));
        var banheiros = Enumerable.Range(1, 4).Select(numero => Comodo("Banho", 3m, 7m, Luz(100m), Tug(600m)) with { Chave = $"b{numero}" }).ToArray();

        var sozinha = Avaliar([cozinha], ("Cozinha", Cozinha)).Comodos.Single();
        var comBanheiros = Avaliar([cozinha, .. banheiros], ("Cozinha", Cozinha), ("Banho", "Banheiro"));

        await Assert.That(sozinha.Situacao).IsEqualTo(SituacaoDoComodo.NaoAtende);
        await Assert.That(sozinha.Faltas.Single()).IsEqualTo("2 pontos de tomada com 600 VA ou mais, de 3 exigidos");
        await Assert.That(comBanheiros.PontosNoConjunto).IsEqualTo(7);
        var cozinhaNoConjunto = comBanheiros.Comodos.Single(avaliacao => avaliacao.Comodo.Nome == "Cozinha");
        await Assert.That(cozinhaNoConjunto.Situacao).IsEqualTo(SituacaoDoComodo.AtendePelaAlternativa);
        await Assert.That(cozinhaNoConjunto.Observacoes[0]).Contains("atende pela alternativa de 2, admitida com mais de 6 pontos no conjunto desses cômodos (7 nos cômodos avaliados");
    }

    [Test]
    public async Task Dormitorio_sem_luz_e_com_poucas_tomadas_lista_as_faltas()
    {
        // 12 m², 14 m de perímetro: 160 VA (100 + 60 · ⌊6 / 4⌋) e 3 pontos (a cada 5 m ou fração).
        var dormitorio = Comodo("Quarto", 12m, 14m, Tug(100m), Tug(80m), Tue(1500m));

        var avaliacao = Avaliar([dormitorio], ("Quarto", "Sala ou dormitório")).Comodos.Single();

        await Assert.That(avaliacao.Situacao).IsEqualTo(SituacaoDoComodo.NaoAtende);
        await Assert.That(string.Join("\n", avaliacao.Faltas)).IsEqualTo(string.Join("\n",
            "0 pontos de luz, abaixo do mínimo de 1",
            "iluminação de 0 VA, abaixo dos 160 VA mínimos",
            "2 pontos de tomada, abaixo dos 3 mínimos (um ponto a cada 5 m de perímetro, ou fração)",
            "1 ponto de tomada abaixo de 100 VA"));
        await Assert.That(avaliacao.Tomadas).IsEqualTo(2);
    }

    [Test]
    public async Task Ponto_sem_potencia_e_falta()
    {
        var banheiro = Comodo("Banho", 3m, 7m, Luz(100m), Tug(600m) with { PotenciaVA = null });

        var avaliacao = Avaliar([banheiro], ("Banho", "Banheiro")).Comodos.Single();

        await Assert.That(avaliacao.Situacao).IsEqualTo(SituacaoDoComodo.NaoAtende);
        await Assert.That(avaliacao.Faltas.Single()).IsEqualTo("1 ponto sem AMP_PotenciaInstaladaVA");
    }

    [Test]
    public async Task Sem_categoria_fora_da_habitacao_e_sem_area_nao_sao_avaliados()
    {
        var resultado = Avaliar(
            [Comodo("Loja", 50m, 30m), Comodo("Depósito", 4m, 8m), Comodo("Hall", 0m, 0m) with { Chave = "h" }],
            ("loja", PrevisaoDeCargas.ForaDaHabitacao), ("Hall", "Demais cômodos"));

        await Assert.That(string.Join("|", resultado.Comodos.Select(avaliacao => $"{avaliacao.Comodo.Nome}:{avaliacao.Situacao}")))
            .IsEqualTo("Depósito:SemCategoria|Hall:SemArea|Loja:ForaDaHabitacao");
        await Assert.That(RelatorioDaPrevisao.Resumo(resultado))
            .IsEqualTo("0 avaliado(s): 0 atende(m), 0 pela alternativa de potência, 0 não atende(m); 1 sem categoria; 1 fora da habitação; 1 sem área");
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, item 9.5.3")]
    public async Task Equipamento_acima_de_10_A_num_circuito_com_outros_pontos_e_falta()
    {
        // Chuveiro 5400 VA / 220 V = 24,55 A, no circuito 2 com uma torneira de 1000 VA; o forno trifásico no 3 está sozinho.
        var banheiro = Comodo("Banho", 3m, 7m, Luz(100m), Tug(600m),
            new PontoDoComodo(50, TipoDeCarga.TUE, 5400m, 2, 220m, "2F"), new PontoDoComodo(51, TipoDeCarga.TUE, 1000m, 2, 220m, "2F"),
            new PontoDoComodo(52, TipoDeCarga.TUE, 9000m, 3, 220m, "3F"));

        var resultado = Avaliar(Leitura([banheiro]), ("Banho", "Banheiro"));

        var falta = resultado.Divisao.Single();
        await Assert.That(falta.Circuito).IsEqualTo("QD1-TUE-01");
        await Assert.That(falta.Descricao).IsEqualTo("1 equipamento acima de 10 A (24,55 A) num circuito de 2 pontos: cada um precisa de circuito independente");
        await Assert.That(falta.Pontos).IsEquivalentTo([50L]);
        await Assert.That(RelatorioDaPrevisao.Resumo(resultado)).EndsWith(". Divisão dos circuitos: 1 falta(s) em 1 circuito(s)");
        await Assert.That(RelatorioDaPrevisao.Markdown(resultado)).Contains("- **QD1-TUE-01** (equipamento acima do limite): 1 equipamento acima de 10 A");
    }

    [Test]
    [Property("Fonte", "NBR 5410:2004, item 9.5.3")]
    public async Task Tomada_de_cozinha_com_tomada_da_sala_ou_fora_de_comodo_e_falta()
    {
        var cozinha = Comodo("Cozinha", 9m, 10m, new PontoDoComodo(60, TipoDeCarga.TUG, 600m, 1));
        var sala = Comodo("Sala", 15m, 16m, new PontoDoComodo(61, TipoDeCarga.TUG, 100m, 1));
        var foraDeComodo = new PontoDoComodo(62, TipoDeCarga.TUG, 100m, 1);

        var mesmaCategoria = Avaliar(Leitura([cozinha, Comodo("Copa", 4m, 8m, new PontoDoComodo(63, TipoDeCarga.TUG, 600m, 1))]),
            ("Cozinha", Cozinha), ("Copa", Cozinha));
        var comSala = Avaliar(Leitura([cozinha, sala], foraDeComodo), ("Cozinha", Cozinha), ("Sala", "Sala ou dormitório"));

        await Assert.That(mesmaCategoria.Divisao).IsEmpty();
        var falta = comSala.Divisao.Single();
        await Assert.That(falta.Regra).IsEqualTo("tomadas de cozinha");
        await Assert.That(falta.Descricao).StartsWith("tomadas de cozinha ou área de serviço com 2 pontos de outro tipo ou de outro cômodo");
        await Assert.That(falta.Pontos).IsEquivalentTo([61L, 62L]);
    }

    [Test]
    public async Task Divisao_so_olha_os_comodos_de_habitacao()
    {
        // O mesmo chuveiro com a torneira, mas num cômodo fora da habitação: a 9.5.3 não se aplica.
        var loja = Comodo("Loja", 30m, 22m, new PontoDoComodo(70, TipoDeCarga.TUE, 5400m, 2, 220m, "2F"), new PontoDoComodo(71, TipoDeCarga.TUE, 1000m, 2, 220m, "2F"));

        await Assert.That(Avaliar([loja], ("Loja", PrevisaoDeCargas.ForaDaHabitacao)).Divisao).IsEmpty();
        await Assert.That(Avaliar([loja]).Divisao).IsEmpty();
    }

    [Test]
    public async Task Executar_guarda_as_escolhas_com_as_do_projeto_e_avalia()
    {
        var documento = new DocumentoFalso(new Dictionary<string, string> { ["Banho"] = "Banheiro", ["Varanda"] = "Varanda" });
        var comodos = new[] { Comodo("Banho", 3m, 7m, Luz(100m), Tug(600m)), Comodo("Sala", 15m, 16m) };

        var execucao = PrevisaoDeCargas.Executar(Leitura(comodos), new Dictionary<string, string?> { ["Sala"] = "Sala ou dormitório", ["Varanda"] = "" }, Norma, documento);

        await Assert.That(execucao.Problemas).IsEmpty();
        await Assert.That(documento.Transacoes).IsEqualTo(1);
        await Assert.That(string.Join("|", documento.Gravadas!.OrderBy(par => par.Key).Select(par => $"{par.Key}={par.Value}")))
            .IsEqualTo("Banho=Banheiro|Sala=Sala ou dormitório");
        await Assert.That(execucao.Resultado!.Comodos.Select(avaliacao => avaliacao.Situacao))
            .IsEquivalentTo([SituacaoDoComodo.Atende, SituacaoDoComodo.NaoAtende]);
    }

    [Test]
    public async Task Categoria_fora_da_norma_e_problema_e_nada_e_gravado()
    {
        var documento = new DocumentoFalso(new Dictionary<string, string>());

        var execucao = PrevisaoDeCargas.Executar(Leitura([Comodo("Sala", 15m, 16m)]), new Dictionary<string, string?> { ["Sala"] = "Escritório" }, Norma, documento);

        await Assert.That(execucao.Resultado).IsNull();
        await Assert.That(execucao.Problemas.Single()).IsEqualTo("Sala: categoria 'Escritório' fora da previsão de cargas da norma");
        await Assert.That(documento.Transacoes).IsEqualTo(0);
    }

    [Test]
    public async Task Relatorio_tem_os_criterios_com_a_referencia_e_uma_linha_por_comodo()
    {
        var resultado = Avaliar([Comodo("Quarto", 12m, 14m, Luz(220m), Tug(100m), Tug(100m), Tug(100m)) with { Numero = "3", Pavimento = "Térreo" }],
            ("Quarto", "Sala ou dormitório"));

        var markdown = RelatorioDaPrevisao.Markdown(resultado);
        var csv = RelatorioDaPrevisao.Csv(resultado).Split("\r\n");

        await Assert.That(markdown).Contains("- **Iluminação** (NBR 5410:2004, itens 9.5.2.1.1");
        await Assert.That(markdown).Contains("  - Sala ou dormitório (alínea d): um ponto a cada 5 m de perímetro, ou fração; 100 VA por ponto.");
        await Assert.That(markdown).Contains("| Térreo | Quarto (3) | Sala ou dormitório | 12 | 14 | 160 / 220 | 1 | 3 / 3 | Atende | — |");
        await Assert.That(csv[0]).StartsWith("Pavimento;Cômodo;Número;Categoria;Área (m²)");
        await Assert.That(csv[1]).IsEqualTo("Térreo;Quarto;3;Sala ou dormitório;12;14;160;220;1;3;3;Atende;;");
    }

    private static ResultadoDaPrevisao Avaliar(IReadOnlyList<ComodoDoProjeto> comodos, params (string Nome, string Categoria)[] categorias) =>
        Avaliar(Leitura(comodos), categorias);

    private static ResultadoDaPrevisao Avaliar(LeituraDaPrevisao leitura, params (string Nome, string Categoria)[] categorias) =>
        PrevisaoDeCargas.Avaliar(leitura, categorias.ToDictionary(par => par.Nome, par => par.Categoria), Norma);

    private static LeituraDaPrevisao Leitura(IReadOnlyList<ComodoDoProjeto> comodos, params PontoDoComodo[] fora) =>
        new(comodos, fora, new Dictionary<long, string> { [1] = "QD1-TUG-01", [2] = "QD1-TUE-01" });

    // Ids gerados a partir de 1000: os testes da divisão usam Ids fixos abaixo disso.
    private static long _proximo = 1000;

    private static ComodoDoProjeto Comodo(string nome, decimal areaM2, decimal perimetroM, params PontoDoComodo[] pontos) =>
        new(nome, nome, null, null, areaM2, perimetroM, pontos);

    private static PontoDoComodo Luz(decimal potenciaVA) => new(Interlocked.Increment(ref _proximo), TipoDeCarga.Iluminacao, potenciaVA);

    private static PontoDoComodo Tug(decimal potenciaVA) => new(Interlocked.Increment(ref _proximo), TipoDeCarga.TUG, potenciaVA);

    private static PontoDoComodo Tue(decimal potenciaVA) => new(Interlocked.Increment(ref _proximo), TipoDeCarga.TUE, potenciaVA);

    private sealed class DocumentoFalso(IReadOnlyDictionary<string, string> guardadas) : IDocumentoDePrevisao
    {
        public int Transacoes { get; private set; }

        public IReadOnlyDictionary<string, string>? Gravadas { get; private set; }

        public void EmUmaTransacao(string nome, Action acao)
        {
            Transacoes++;
            acao();
        }

        public LeituraDaPrevisao Ler() => new([], [], new Dictionary<long, string>());

        public IReadOnlyDictionary<string, string> LerCategorias() => guardadas;

        public string? GravarCategorias(IReadOnlyDictionary<string, string> categoriaPorNome)
        {
            Gravadas = categoriaPorNome;
            return null;
        }
    }
}

public class CategoriasEmJson_Teste
{
    [Test]
    public async Task GUID_do_esquema_de_armazenamento_esta_congelado()
    {
        // Os projetos guardam as categorias presas a este GUID: trocá-lo deixaria os dados gravados órfãos (AGENTS.md).
        await Assert.That(Guid.Parse(CategoriasEmJson.GuidDoEsquema)).IsEqualTo(Guid.Parse("cda48f7f-9288-488d-bbca-997e5456bc77"));
    }

    [Test]
    public async Task Texto_canonico_em_ordem_sem_vazios_e_sem_nomes_repetidos_por_maiusculas()
    {
        var json = CategoriasEmJson.Escrever(new Dictionary<string, string> { ["sala"] = "Sala ou dormitório", ["Banho "] = "Banheiro", ["Sala"] = "Varanda", ["Hall"] = " " });

        await Assert.That(json).IsEqualTo("{\"versao\":1,\"categorias\":{\"Banho\":\"Banheiro\",\"Sala\":\"Varanda\"}}");
        await Assert.That(CategoriasEmJson.Ler(json)!["banho"]).IsEqualTo("Banheiro");
    }

    [Test]
    public async Task Acentos_vao_e_voltam()
    {
        var categorias = new Dictionary<string, string> { ["Área de serviço"] = "Cozinha, copa, área de serviço ou lavanderia" };

        await Assert.That(CategoriasEmJson.Ler(CategoriasEmJson.Escrever(categorias))!["Área de serviço"]).IsEqualTo("Cozinha, copa, área de serviço ou lavanderia");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("não é JSON")]
    [Arguments("{\"versao\":2,\"categorias\":{}}")]
    [Arguments("{\"versao\":1}")]
    [Arguments("{\"versao\":1,\"categorias\":{\"Sala\":1}}")]
    public async Task Texto_vazio_invalido_ou_de_outra_versao_nao_vira_categoria(string? json)
    {
        await Assert.That(CategoriasEmJson.Ler(json)).IsNull();
    }
}
