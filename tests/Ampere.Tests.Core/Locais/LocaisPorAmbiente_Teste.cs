using Ampere.Core.Locais;
using TUnit.Assertions.Enums;

namespace Ampere.Tests.Core.Locais;

public class LocaisPorAmbiente_Teste
{
    private const string Banheiro = "Local com banheira ou chuveiro";
    private const string Demais = "Demais locais internos";
    private static readonly string[] LocaisDoPerfil = [Banheiro, Demais, "Area externa"];

    private static readonly IReadOnlyList<PontoNoAmbiente> Pontos =
    [
        new(1, "Banho", null),
        new(2, "banho ", Banheiro),
        new(3, "Sala", Demais),
        new(4, "Sala", Demais),
        new(5, "Cozinha", null),
        new(6, null, null),
        new(7, "  ", null)
    ];

    [Test]
    public async Task Agrupa_por_nome_de_ambiente_sem_diferenciar_maiusculas_e_traz_o_local_comum()
    {
        var ambientes = LocaisPorAmbiente.Agrupar(Pontos);

        await Assert.That(ambientes).IsEquivalentTo(
            [new AmbienteComPontos("Banho", 2, null), new AmbienteComPontos("Cozinha", 1, null), new AmbienteComPontos("Sala", 2, Demais)],
            CollectionOrdering.Matching);
    }

    [Test]
    public async Task Grava_o_local_escolhido_so_onde_muda_numa_transacao()
    {
        var documento = new DocumentoFalso();

        var resultado = LocaisPorAmbiente.Aplicar(Pontos, new Dictionary<string, string> { ["BANHO"] = Banheiro, ["Sala"] = Demais }, LocaisDoPerfil, documento);

        await Assert.That(resultado).IsEqualTo(new ResultadoDosLocais(1, 3, 1, 2, 0, []));
        await Assert.That(documento.Gravados).IsEquivalentTo([new LocalParaGravar(1, Banheiro)]);
        await Assert.That(documento.Transacoes).IsEqualTo(1);
    }

    [Test]
    public async Task Ponto_que_nao_aceita_edicao_fica_de_fora_e_e_contado()
    {
        var documento = new DocumentoFalso();
        PontoNoAmbiente[] pontos = [new(1, "Banho", null, Editavel: false), new(2, "Banho", null)];

        var resultado = LocaisPorAmbiente.Aplicar(pontos, new Dictionary<string, string> { ["Banho"] = Banheiro }, LocaisDoPerfil, documento);

        await Assert.That(resultado.NaoEditaveis).IsEqualTo(1);
        await Assert.That(documento.Gravados).IsEquivalentTo([new LocalParaGravar(2, Banheiro)]);
    }

    [Test]
    public async Task Local_fora_do_perfil_nao_grava_nada()
    {
        var documento = new DocumentoFalso();

        var resultado = LocaisPorAmbiente.Aplicar(Pontos, new Dictionary<string, string> { ["Banho"] = "Banheiro" }, LocaisDoPerfil, documento);

        await Assert.That(resultado.Problemas).IsEquivalentTo(["Banho: local 'Banheiro' fora da tabela de proteção diferencial do perfil"]);
        await Assert.That(documento.Transacoes).IsEqualTo(0);
    }

    [Test]
    public async Task Sem_nada_a_mudar_nao_abre_transacao()
    {
        var documento = new DocumentoFalso();

        var resultado = LocaisPorAmbiente.Aplicar(Pontos, new Dictionary<string, string> { ["Sala"] = Demais, ["Cozinha"] = " " }, LocaisDoPerfil, documento);

        await Assert.That(resultado.Gravados).IsEqualTo(0);
        await Assert.That(resultado.JaEstavam).IsEqualTo(2);
        await Assert.That(documento.Transacoes).IsEqualTo(0);
    }

    private sealed class DocumentoFalso : IDocumentoDeAmbientes
    {
        public int Transacoes { get; private set; }

        public List<LocalParaGravar> Gravados { get; } = [];

        public void EmUmaTransacao(string nome, Action acao)
        {
            Transacoes++;
            acao();
        }

        public IReadOnlyList<PontoNoAmbiente> LerPontos(IReadOnlyCollection<long> ids) => Pontos;

        public void GravarLocais(IReadOnlyList<LocalParaGravar> locais) => Gravados.AddRange(locais);
    }
}
