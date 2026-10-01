using Ampere.Core.Cargas;
using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Core.Quadros;
using Ampere.Core.Verificacao;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;

namespace Ampere.Tests.Core.Verificacao;

/// <summary>Verificação do projeto (perfil e catálogos fictícios): só leitura, pendências em ordem de gravidade.</summary>
public class VerificacaoDoProjeto_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);
    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    private static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    [Test]
    public async Task Projeto_em_dia_nao_tem_pendencias()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)));
        documento.GravarMemoriasAtuais();

        var relatorio = Verificar(documento);

        await Assert.That(relatorio.Pendencias).IsEmpty();
        await Assert.That(relatorio.MemoriasConferidas).IsTrue();
        await Assert.That(relatorio.Pontos).IsEqualTo(1);
        await Assert.That(relatorio.Circuitos).IsEqualTo(1);
    }

    [Test]
    public async Task Pontos_sem_classificacao_fora_de_circuito_ou_sem_local()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11), Ponto(12, local: null)));
        documento.Pontos.Add(new PontoVerificado(20, null, null, null));
        documento.Pontos.Add(new PontoVerificado(21, "TUG", null, null));
        documento.Pontos.Add(new PontoVerificado(22, "Reserva", "LOCAL-SECO", null));
        documento.GravarMemoriasAtuais();

        var relatorio = Verificar(documento);

        await Assert.That(Grupos(relatorio)).IsEqualTo(
            $"Erro|{VerificacaoDoProjeto.PontosForaDeCircuito}|21\n" +
            $"Erro|{VerificacaoDoProjeto.PontosSemLocal}|12\n" +
            $"Aviso|{VerificacaoDoProjeto.PontosSemClassificacao}|20;22\n" +
            $"Informacao|{VerificacaoDoProjeto.CalculoInterrompido}|1");
    }

    [Test]
    public async Task Ponto_sem_local_num_circuito_com_decisao_do_projetista_sobre_o_IDR_nao_e_pendencia()
    {
        var circuito = Circuito(1, "TUG-01", Ponto(11, local: null)) with { Decisoes = new DecisoesDoProjetista(Idr: "Dispensar") };
        var documento = Documento(circuito);
        documento.GravarMemoriasAtuais();

        var relatorio = Verificar(documento);

        await Assert.That(relatorio.Pendencias.Where(pendencia => pendencia.Grupo == VerificacaoDoProjeto.PontosSemLocal)).IsEmpty();
    }

    [Test]
    public async Task Circuito_criado_fora_do_Ampere_com_pontos_classificados()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)));
        documento.Circuitos.Add(new CircuitoVerificado(2, null, null, "Circuito 7", null));
        documento.Pontos.Add(new PontoVerificado(21, "TUG", "LOCAL-SECO", 2));
        documento.GravarMemoriasAtuais();

        var pendencia = Verificar(documento).Pendencias.Single();

        await Assert.That(pendencia.Gravidade).IsEqualTo(GravidadeDaPendencia.Aviso);
        await Assert.That(pendencia.Grupo).IsEqualTo(VerificacaoDoProjeto.CircuitosForaDoAmpere);
        await Assert.That(pendencia.Descricao).EndsWith("— Circuito 7");
        await Assert.That(pendencia.Elementos).IsEquivalentTo([2L]);
    }

    [Test]
    public async Task Memoria_sem_as_condicoes_da_rodada_nao_e_conferida_e_o_relatorio_diz_por_que()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)), Circuito(2, "TUG-02", Ponto(21)));
        documento.GravarMemoriasAtuais();
        documento.CondicoesDosCircuitos.Remove(1);
        documento.Condicoes = null;
        documento.Gravar(2, null);
        documento.CondicoesDosCircuitos.Remove(2);

        var relatorio = Verificar(documento);

        await Assert.That(relatorio.MemoriasConferidas).IsFalse();
        await Assert.That(Grupos(relatorio)).IsEqualTo(
            $"Aviso|{VerificacaoDoProjeto.CircuitosNaoDimensionados}|2\n" +
            $"Informacao|{VerificacaoDoProjeto.CondicoesNaoGuardadas}|1");
    }

    [Test]
    public async Task Cada_circuito_e_conferido_com_as_condicoes_da_rodada_que_o_dimensionou()
    {
        // O projeto todo a 30 °C e depois só o TUG-02 a 40 °C: nenhuma memória está desatualizada.
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)), Circuito(2, "TUG-02", Ponto(21)));
        documento.GravarMemoriasAtuais();
        documento.Condicoes = Condicoes with { TemperaturaAmbienteC = 40m };
        documento.GravarMemoriasAtuais(2);
        documento.Condicoes = Condicoes;

        var relatorio = Verificar(documento);

        await Assert.That(relatorio.Pendencias).IsEmpty();
        await Assert.That(relatorio.MemoriasConferidas).IsTrue();
    }

    [Test]
    public async Task Entrada_invalida_e_erro_com_o_motivo_e_nao_circuito_por_dimensionar()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)) with { ComprimentoM = -5m });
        documento.GravarMemoriasAtuais();

        var pendencia = Verificar(documento).Pendencias.Single();

        await Assert.That(pendencia.Gravidade).IsEqualTo(GravidadeDaPendencia.Erro);
        await Assert.That(pendencia.Grupo).IsEqualTo(VerificacaoDoProjeto.CircuitosComDadosFaltando);
        await Assert.That(pendencia.Descricao).IsEqualTo("QD1 TUG-01: comprimento deve ser positivo");
    }

    [Test]
    public async Task Circuito_nunca_dimensionado_e_memoria_desatualizada()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)), Circuito(2, "TUG-02", Ponto(21)), Circuito(3, "TUG-03", Ponto(31)));
        documento.GravarMemoriasAtuais();
        documento.Gravar(1, null);
        documento.Gravar(2, "sha256:de-uma-rodada-anterior");

        var relatorio = Verificar(documento);

        await Assert.That(Grupos(relatorio)).IsEqualTo(
            $"Aviso|{VerificacaoDoProjeto.CircuitosNaoDimensionados}|1\n" +
            $"Aviso|{VerificacaoDoProjeto.MemoriasDesatualizadas}|2");
        await Assert.That(relatorio.Pendencias[1].Descricao).EndsWith("— QD1 TUG-02");
    }

    [Test]
    public async Task Mudanca_nos_dados_do_circuito_desatualiza_a_memoria()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)));
        documento.GravarMemoriasAtuais();
        documento.Trocar(Circuito(1, "TUG-01", Ponto(11)) with { ComprimentoM = 25m });

        var relatorio = Verificar(documento);

        await Assert.That(relatorio.Pendencias.Single().Grupo).IsEqualTo(VerificacaoDoProjeto.MemoriasDesatualizadas);
    }

    [Test]
    public async Task Circuito_com_dados_faltando_e_erro_com_o_motivo()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)) with { ComprimentoM = null });

        var pendencia = Verificar(documento).Pendencias.Single();

        await Assert.That(pendencia.Gravidade).IsEqualTo(GravidadeDaPendencia.Erro);
        await Assert.That(pendencia.Grupo).IsEqualTo(VerificacaoDoProjeto.CircuitosComDadosFaltando);
        await Assert.That(pendencia.Descricao).IsEqualTo("QD1 TUG-01: sem AMP_ComprimentoRotaM e sem comprimento do circuito no Revit");
    }

    [Test]
    public async Task Onde_o_calculo_para_e_avisos_do_dimensionamento()
    {
        var dispensado = Circuito(2, "TUG-02", Ponto(21, local: "LOCAL-MOLHADO")) with { Decisoes = new DecisoesDoProjetista(Idr: "Dispensar") };
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)), dispensado);
        documento.Condicoes = Condicoes with { TipoDeEletroduto = null };
        documento.GravarMemoriasAtuais();

        var relatorio = Verificar(documento);

        await Assert.That(Grupos(relatorio)).IsEqualTo(
            $"Aviso|{VerificacaoDoProjeto.AvisosDoDimensionamento}|2\n" +
            $"Informacao|{VerificacaoDoProjeto.CalculoInterrompido}|1;2");
        await Assert.That(relatorio.Pendencias[1].Descricao).IsEqualTo("tipo de eletroduto não informado (nas condições do projeto) (2 circuito(s))");
    }

    [Test]
    public async Task Relatorio_em_Markdown_por_gravidade_e_grupo_sem_data()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11), Ponto(12, local: null)));
        documento.Pontos.Add(new PontoVerificado(20, null, null, null));
        documento.GravarMemoriasAtuais();

        var texto = Verificar(documento).Markdown();

        await Assert.That(texto).StartsWith("# Verificação do projeto\n\n- **Pontos de carga verificados:** 3\n");
        await Assert.That(texto).Contains("- **Pendências:** 1 erro(s), 1 aviso(s), 1 informação(ões)\n");
        await Assert.That(texto).Contains("\n## Erros\n\n### Pontos sem local\n\n- 1 ponto(s) sem AMP_Local");
        await Assert.That(texto).Contains("(elementos: 12)\n");
        await Assert.That(texto.IndexOf("## Erros", StringComparison.Ordinal)).IsLessThan(texto.IndexOf("## Avisos", StringComparison.Ordinal));
        await Assert.That(texto).IsEqualTo(Verificar(documento).Markdown());
    }

    [Test]
    public async Task Relatorio_sem_pendencias_diz_isso()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)));
        documento.GravarMemoriasAtuais();

        await Assert.That(Verificar(documento).Markdown()).EndsWith("\nSem pendências.\n");
    }

    [Test]
    public async Task Quadro_de_cargas_conferido_pelos_fatores_guardados()
    {
        var documento = Documento(Circuito(1, "TUG-01", Ponto(11)));
        documento.GravarMemoriasAtuais();
        var fatores = new Dictionary<TipoDeCarga, decimal> { [TipoDeCarga.TUG] = 0.5m };
        var lido = new QuadroLido(50, "QD1", [new CircuitoLido(1, "TUG-01", "TUG", 1270m, "F+N", 127m)]);
        var emDia = new QuadrosFalsos(lido, QuadroDeCargasDoProjeto.Montar(lido, Ficticio, fatores).Quadro.Memoria!.Hash(), fatores);

        await Assert.That(VerificacaoDoProjeto.Executar(documento, Ficticio, Catalogos, emDia).Pendencias).IsEmpty();
        await Assert.That(Grupos(VerificacaoDoProjeto.Executar(documento, Ficticio, Catalogos, emDia with { Memoria = null })))
            .IsEqualTo($"Aviso|{VerificacaoDoProjeto.QuadrosNaoMontados}|50");
        await Assert.That(Grupos(VerificacaoDoProjeto.Executar(documento, Ficticio, Catalogos, emDia with { Memoria = "sha256:antiga" })))
            .IsEqualTo($"Aviso|{VerificacaoDoProjeto.QuadrosDesatualizados}|50");
        var semFatores = VerificacaoDoProjeto.Executar(documento, Ficticio, Catalogos, emDia with { Fatores = null });
        await Assert.That(Grupos(semFatores)).IsEqualTo($"Informacao|{VerificacaoDoProjeto.CondicoesNaoGuardadas}|50");
        await Assert.That(semFatores.MemoriasConferidas).IsFalse();
    }

    private sealed record QuadrosFalsos(QuadroLido Quadro, string? Memoria, IReadOnlyDictionary<TipoDeCarga, decimal>? Fatores) : IDocumentoDeQuadros
    {
        public void EmUmaTransacao(string nome, Action acao) => throw new NotSupportedException();

        public IReadOnlyList<QuadroLido> LerQuadrosComCircuitos() => [Quadro];

        public string? LerMemoriaDoQuadro(long quadroId) => Memoria;

        public IReadOnlyDictionary<TipoDeCarga, decimal>? LerFatoresDoQuadro(long quadroId) => Fatores;

        public void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas) => throw new NotSupportedException();

        public bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria, IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados) =>
            throw new NotSupportedException();

        public IReadOnlyList<string> ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados) => throw new NotSupportedException();

        public IReadOnlyList<CircuitoLido> LerCircuitosSemQuadro() => throw new NotSupportedException();

        public string CriarTabelaDoQuadro(string nomeDoQuadro) => throw new NotSupportedException();
    }

    private static RelatorioDeVerificacao Verificar(DocumentoFalso documento) => VerificacaoDoProjeto.Executar(documento, Ficticio, Catalogos);

    private static string Grupos(RelatorioDeVerificacao relatorio) =>
        string.Join("\n", relatorio.Pendencias.Select(pendencia => $"{pendencia.Gravidade}|{pendencia.Grupo}|{string.Join(";", pendencia.Elementos)}"));

    private static DocumentoFalso Documento(params DadosDoCircuito[] circuitos) => new(circuitos) { Condicoes = Condicoes };

    private static DadosDoCircuito Circuito(long id, string numero, params DadosDoPonto[] pontos) =>
        new(id, numero, "TUG", 10m, null, null, pontos, Quadro: "QD1");

    private static DadosDoPonto Ponto(long id, string? local = "LOCAL-SECO") => new(id, 1270m, 127m, "F+N", local, "TUG");

    private sealed class DocumentoFalso : IDocumentoDeVerificacao
    {
        private readonly List<DadosDoCircuito> _circuitos;

        public DocumentoFalso(IEnumerable<DadosDoCircuito> circuitos)
        {
            _circuitos = circuitos.ToList();
            Pontos = _circuitos.SelectMany(circuito => circuito.Pontos.Select(ponto => new PontoVerificado(ponto.Id, ponto.TipoDeCarga, ponto.Local, circuito.Id))).ToList();
            Circuitos = _circuitos.Select(circuito => new CircuitoVerificado(circuito.Id, circuito.Numero, circuito.Quadro, $"Circuito {circuito.Id}", null)).ToList();
        }

        public List<PontoVerificado> Pontos { get; }

        public List<CircuitoVerificado> Circuitos { get; }

        public CondicoesDoProjeto? Condicoes { get; set; }

        public IReadOnlyList<PontoVerificado> LerPontos() => Pontos;

        public IReadOnlyList<CircuitoVerificado> LerCircuitosDeForca() => Circuitos;

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => _circuitos.Where(circuito => ids.Contains(circuito.Id)).ToList();

        public CondicoesDoProjeto? LerCondicoes() => Condicoes;

        public Dictionary<long, CondicoesDoProjeto> CondicoesDosCircuitos { get; } = [];

        public IReadOnlyDictionary<long, CondicoesDoProjeto> LerCondicoesDosCircuitos(IReadOnlyCollection<long> ids) =>
            CondicoesDosCircuitos.Where(par => ids.Contains(par.Key)).ToDictionary(par => par.Key, par => par.Value);

        /// <summary>Como depois de uma rodada do "Dimensionar" (pelo próprio caso de uso) com as condições atuais.</summary>
        public void GravarMemoriasAtuais(params long[] so)
        {
            var ids = so.Length > 0 ? so : _circuitos.Select(circuito => circuito.Id).ToArray();
            var documento = new DimensionamentoFalso(_circuitos);
            foreach (var resultado in DimensionamentoDoProjeto.Executar(ids, Condicoes!, Ficticio, Catalogos, documento, doProjetoTodo: so.Length == 0))
            {
                Gravar(resultado.Id, resultado.Memoria?.Hash());
                CondicoesDosCircuitos[resultado.Id] = Condicoes!;
            }
        }

        private sealed class DimensionamentoFalso(IReadOnlyList<DadosDoCircuito> circuitos) : IDocumentoDeDimensionamento
        {
            public void EmUmaTransacao(string nome, Action acao) => acao();

            public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids) => circuitos.Where(circuito => ids.Contains(circuito.Id)).ToList();

            public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados)
            {
            }

            public void GravarCondicoes(CondicoesDoProjeto condicoes, IReadOnlyCollection<long> circuitos, bool doProjetoTodo)
            {
            }
        }

        public void Trocar(DadosDoCircuito circuito) => _circuitos[_circuitos.FindIndex(existente => existente.Id == circuito.Id)] = circuito;

        public void Gravar(long circuito, string? memoria)
        {
            var indice = Circuitos.FindIndex(verificado => verificado.Id == circuito);
            Circuitos[indice] = Circuitos[indice] with { MemoriaGravada = memoria };
        }
    }
}
