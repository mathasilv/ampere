using Ampere.Core.Catalogos;
using Ampere.Core.Dimensionamento;
using Ampere.Core.Normas;
using Ampere.Tests.Core.Catalogos;
using Ampere.Tests.Core.Normas;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Dimensionamento;

public class DimensionamentoDoProjeto_Teste
{
    private static readonly PerfilNormativo Ficticio = PerfilNormativo.Carregar(PerfilFicticio.Json);
    private static readonly CondicoesDoProjeto Condicoes = new(30m, 1, "Cobre", "B1", "PVC", CatalogosFicticios.TipoDeCondutor, CatalogosFicticios.TipoDeEletroduto);

    private static readonly CatalogosDeProduto Catalogos = new(
        CatalogoDeCondutores.Carregar(CatalogosFicticios.Condutores),
        CatalogoDeEletrodutos.Carregar(CatalogosFicticios.Eletrodutos));

    [Test]
    public async Task Dez_circuitos_mistos_dimensionados_com_memoria_100_por_cento_referenciada()
    {
        var documento = new DocumentoDeDimensionamentoFalso(DezCircuitos());

        var resultados = DimensionamentoDoProjeto.Executar([.. documento.Ids], Condicoes, Ficticio, Catalogos, documento);

        await Assert.That(resultados.Count).IsEqualTo(10);
        await Assert.That(resultados.All(resultado => resultado.Dimensionamento?.Situacao == SituacaoDoDimensionamento.Dimensionado)).IsTrue();

        var passos = resultados.SelectMany(resultado => resultado.Dimensionamento!.Memoria!.Passos).ToList();
        await Assert.That(passos.Where(passo => string.IsNullOrWhiteSpace(passo.Referencia))).IsEmpty();
        await Assert.That(passos.Where(passo => !passo.Referencia.StartsWith("FICTÍCIO"))).IsEmpty();

        await Assert.That(resultados.Select(resultado => resultado.Dimensionamento!.Memoria!.Hash()).Distinct().Count()).IsEqualTo(10);
        await Assert.That(documento.Chamadas).IsEquivalentTo(
            ["ler:10", "transacao:" + DimensionamentoDoProjeto.NomeDaTransacao, "gravar:10"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Potencia_e_a_soma_dos_pontos_e_tensao_e_fases_sao_as_comuns()
    {
        var circuito = Circuito(1, "TUG-01", "TUG", Ponto(11, 600m), Ponto(12, 670m));

        var resultado = DimensionamentoDoProjeto.Executar([1], Condicoes, Ficticio, Catalogos, new DocumentoDeDimensionamentoFalso([circuito]))[0];

        await Assert.That(resultado.Dimensionamento!.CorrenteDeProjetoA).IsEqualTo(10m);
        await Assert.That(resultado.Dimensionamento.Memoria!.Passos[0].Valores[0].Valor).IsEqualTo(1270m);
    }

    [Test]
    public async Task Pontos_com_tensoes_diferentes_viram_problema_de_dados()
    {
        var circuito = Circuito(1, "TUG-01", "TUG", Ponto(11, 600m), Ponto(12, 600m, tensaoV: 220m));

        var resultado = DimensionamentoDoProjeto.Executar([1], Condicoes, Ficticio, Catalogos, new DocumentoDeDimensionamentoFalso([circuito]))[0];

        await Assert.That(resultado.Dimensionamento).IsNull();
        await Assert.That(string.Join("\n", resultado.ProblemasDeDados)).Contains("pontos com tensões diferentes (127, 220)");
    }

    [Test]
    public async Task Dados_faltando_sao_todos_relatados_e_nada_e_gravado()
    {
        var circuito = new DadosDoCircuito(1, null, "Tomada", null, null, null, [new DadosDoPonto(11, null, null, null)]);
        var documento = new DocumentoDeDimensionamentoFalso([circuito]);

        var resultado = DimensionamentoDoProjeto.Executar([1], Condicoes with { MetodoDeInstalacaoPadrao = null }, Ficticio, Catalogos, documento)[0];

        var problemas = string.Join("\n", resultado.ProblemasDeDados);
        await Assert.That(problemas).Contains("sem AMP_NumeroCircuito");
        await Assert.That(problemas).Contains("AMP_TipoCarga vazio ou desconhecido ('Tomada')");
        await Assert.That(problemas).Contains("sem AMP_ComprimentoRotaM");
        await Assert.That(problemas).Contains("sem AMP_MetodoInstalacao nem padrão do projeto");
        await Assert.That(problemas).Contains("ponto 11 sem AMP_PotenciaInstaladaVA");
        await Assert.That(documento.Chamadas).IsEquivalentTo(["ler:1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Sem_tipo_de_eletroduto_no_projeto_nem_tipo_de_condutor_sao_problemas_de_dados()
    {
        var circuito = Circuito(1, "TUG-01", "TUG", Ponto(11, 1270m));
        var semTipos = Condicoes with { TipoDeCondutorPadrao = null, TipoDeEletroduto = null };

        var resultado = DimensionamentoDoProjeto.Executar([1], semTipos, Ficticio, Catalogos, new DocumentoDeDimensionamentoFalso([circuito]))[0];

        var problemas = string.Join("\n", resultado.ProblemasDeDados);
        await Assert.That(problemas).Contains("sem AMP_TipoCondutor nem padrão do projeto");
        await Assert.That(problemas).Contains("tipo de eletroduto do projeto não informado");
    }

    [Test]
    public async Task Local_dos_pontos_e_decisao_do_projetista_chegam_ao_motor()
    {
        var molhado = Circuito(1, "TUG-01", "TUG", Ponto(11, 600m), Ponto(12, 600m, local: "LOCAL-MOLHADO"));
        var dispensado = Circuito(2, "TUG-02", "TUG", Ponto(21, 600m, local: "LOCAL-MOLHADO")) with { IdrDoProjetista = DecisaoDeIdr.Dispensado() };

        var resultados = DimensionamentoDoProjeto.Executar([1, 2], Condicoes, Ficticio, Catalogos, new DocumentoDeDimensionamentoFalso([molhado, dispensado]));

        await Assert.That(resultados[0].Dimensionamento!.IdrSensibilidadeMa).IsEqualTo(30m);
        await Assert.That(resultados[0].Dimensionamento!.IdrNominalA).IsEqualTo(25m);
        await Assert.That(resultados[1].Dimensionamento!.IdrSensibilidadeMa).IsNull();
        await Assert.That(resultados[1].Dimensionamento!.Avisos.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Padroes_do_projeto_completam_metodo_e_isolacao_vazios()
    {
        var circuito = Circuito(1, "TUG-01", "TUG", Ponto(11, 1270m)) with { MetodoDeInstalacao = null, Isolacao = null };

        var resultado = DimensionamentoDoProjeto.Executar([1], Condicoes, Ficticio, Catalogos, new DocumentoDeDimensionamentoFalso([circuito]))[0];

        await Assert.That(resultado.Dimensionamento!.Situacao).IsEqualTo(SituacaoDoDimensionamento.Dimensionado);
    }

    [Test]
    public async Task Perfil_oficial_grava_o_que_foi_possivel_e_a_memoria_explica_onde_parou()
    {
        var documento = new DocumentoDeDimensionamentoFalso(DezCircuitos());

        var resultados = DimensionamentoDoProjeto.Executar([.. documento.Ids], Condicoes, PerfilNormativo.NBR5410_2004, Catalogos, documento);

        await Assert.That(resultados.All(resultado => resultado.Dimensionamento!.Situacao == SituacaoDoDimensionamento.Interrompido)).IsTrue();
        await Assert.That(resultados.All(resultado => resultado.Dimensionamento!.CorrenteDeProjetoA is not null)).IsTrue();
        await Assert.That(documento.Chamadas).IsEquivalentTo(
            ["ler:10", "transacao:" + DimensionamentoDoProjeto.NomeDaTransacao, "gravar:10"], CollectionOrdering.Matching);
    }

    private static List<DadosDoCircuito> DezCircuitos()
    {
        var circuitos = new List<DadosDoCircuito>();
        for (var indice = 1; indice <= 2; indice++) circuitos.Add(Circuito(indice, $"IL-0{indice}", "Iluminação", Ponto(indice * 100 + 1, 100m), Ponto(indice * 100 + 2, 100m)));
        for (var indice = 1; indice <= 3; indice++) circuitos.Add(Circuito(10 + indice, $"TUG-0{indice}", "TUG", Ponto(1000 + indice, 600m), Ponto(1100 + indice, 600m)));
        for (var indice = 1; indice <= 5; indice++) circuitos.Add(Circuito(20 + indice, $"TUE-0{indice}", "TUE", Ponto(2000 + indice, 1200m)));
        return circuitos;
    }

    private static DadosDoCircuito Circuito(long id, string numero, string tipo, params DadosDoPonto[] pontos) =>
        new(id, numero, tipo, 10m, "B1", "PVC", pontos);

    private static DadosDoPonto Ponto(long id, decimal potenciaVA, decimal tensaoV = 127m, string fases = "F+N", string? local = "LOCAL-SECO") =>
        new(id, potenciaVA, tensaoV, fases, local);

    private sealed class DocumentoDeDimensionamentoFalso(IReadOnlyList<DadosDoCircuito> circuitos) : IDocumentoDeDimensionamento
    {
        public List<string> Chamadas { get; } = [];

        public IEnumerable<long> Ids => circuitos.Select(circuito => circuito.Id);

        public void EmUmaTransacao(string nome, Action acao)
        {
            Chamadas.Add($"transacao:{nome}");
            acao();
        }

        public IReadOnlyList<DadosDoCircuito> LerCircuitos(IReadOnlyCollection<long> ids)
        {
            Chamadas.Add($"ler:{ids.Count}");
            return circuitos.Where(circuito => ids.Contains(circuito.Id)).ToList();
        }

        public void GravarResultados(IReadOnlyList<ResultadoDoCircuito> resultados) => Chamadas.Add($"gravar:{resultados.Count}");
    }
}
