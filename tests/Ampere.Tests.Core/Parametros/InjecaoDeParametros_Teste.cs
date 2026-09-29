using Ampere.Core.Parametros;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Parametros;

public class InjecaoDeParametros_Teste
{
    private const string Transacao = "transacao:" + InjecaoDeParametros.NomeDaTransacao;

    [Test]
    public async Task Documento_limpo_cria_tudo_em_uma_transacao_e_um_unico_lote()
    {
        var catalogo = CatalogoDeParametros.Padrao;
        var documento = new DocumentoFalso([]);

        InjecaoDeParametros.Executar(catalogo, documento);

        var nomes = string.Join(",", catalogo.Parametros.Select(parametro => parametro.Nome));
        await Assert.That(documento.Chamadas).IsEquivalentTo(["ler", Transacao, $"criar:{nomes}@Ampere"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Criar_vem_antes_de_ampliar_e_ambos_ficam_dentro_da_mesma_transacao()
    {
        var catalogo = CatalogoDeParametros.Carregar("""
            {
              "$meta": { "fonte": "teste" },
              "produto": "Ampere",
              "grupo_revit": "Grupo",
              "parametros": [
                { "guid": "11111111-1111-1111-1111-111111111111", "nome": "AMP_A", "tipo": "TEXT", "descricao": "", "categorias": ["CircuitosEletricos"] },
                { "guid": "22222222-2222-2222-2222-222222222222", "nome": "AMP_B", "tipo": "TEXT", "descricao": "", "categorias": ["Luminarias", "CircuitosEletricos"] }
              ]
            }
            """);
        var parcial = new ParametroExistente("AMP_B", Guid.Parse("22222222-2222-2222-2222-222222222222"), TipoDeDadoDoParametro.Texto,
            new VinculoExistente(true, [CategoriaEletrica.CircuitosEletricos]));
        var documento = new DocumentoFalso([parcial]);

        InjecaoDeParametros.Executar(catalogo, documento);

        await Assert.That(documento.Chamadas).IsEquivalentTo(["ler", Transacao, "criar:AMP_A@Grupo", "ampliar:AMP_B:Luminarias"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Conflito_aborta_sem_abrir_transacao_nem_alterar_nada()
    {
        var homonimo = new ParametroExistente("AMP_TipoCarga", null, TipoDeDadoDoParametro.Texto, null);
        var documento = new DocumentoFalso([homonimo]);

        var plano = InjecaoDeParametros.Executar(CatalogoDeParametros.Padrao, documento);

        await Assert.That(plano.TemConflitos).IsTrue();
        await Assert.That(documento.Chamadas).IsEquivalentTo(["ler"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Documento_ja_conforme_nao_abre_transacao_para_nao_criar_desfazer_vazio()
    {
        var conformes = CatalogoDeParametros.Padrao.Parametros
            .Select(parametro => new ParametroExistente(parametro.Nome, parametro.Guid, parametro.Tipo,
                new VinculoExistente(true, parametro.Categorias)))
            .ToList();
        var documento = new DocumentoFalso(conformes);

        var plano = InjecaoDeParametros.Executar(CatalogoDeParametros.Padrao, documento);

        await Assert.That(plano.NadaAFazer).IsTrue();
        await Assert.That(documento.Chamadas).IsEquivalentTo(["ler"], CollectionOrdering.Matching);
    }

    /// <summary>Registra as chamadas em ordem e denuncia escrita fora de transação.</summary>
    private sealed class DocumentoFalso(IReadOnlyCollection<ParametroExistente> existentes) : IParametrosDoDocumento
    {
        private bool _emTransacao;

        public List<string> Chamadas { get; } = [];

        public IReadOnlyCollection<ParametroExistente> LerExistentes()
        {
            Chamadas.Add("ler");
            return existentes;
        }

        public void EmUmaTransacao(string nome, Action acao)
        {
            Chamadas.Add($"transacao:{nome}");
            _emTransacao = true;
            try
            {
                acao();
            }
            finally
            {
                _emTransacao = false;
            }
        }

        public void Criar(IReadOnlyList<DefinicaoDeParametro> definicoes, string grupoRevit) =>
            Chamadas.Add($"criar:{string.Join(",", definicoes.Select(definicao => definicao.Nome))}@{grupoRevit}{ForaDaTransacao()}");

        public void AmpliarCategorias(DefinicaoDeParametro definicao, IReadOnlyList<CategoriaEletrica> faltantes) =>
            Chamadas.Add($"ampliar:{definicao.Nome}:{string.Join(",", faltantes)}{ForaDaTransacao()}");

        private string ForaDaTransacao() => _emTransacao ? string.Empty : " FORA DA TRANSACAO";
    }
}
