using System.Security.Cryptography;
using System.Text;
using Ampere.Core.Memoria;

namespace Ampere.Tests.Core.Memoria;

public class MemoriaDeCalculo_Teste
{
    [Test]
    public async Task Json_canonico_fixado_e_hash_e_o_sha256_dele()
    {
        var memoria = Exemplo();

        const string esperado =
            """{"esquema":"1","circuito":"TUG-01","perfilNorma":"FICTICIO-TESTE","passos":[{"ref":"FICT\u00CDCIO: se\u00E7\u00F5es","descricao":"Corrente de projeto","expr":"IB = S / V","valores":[{"nome":"S","valor":1270,"unidade":"VA"},{"nome":"V","valor":127,"unidade":"V"}],"resultado":10,"unidade":"A"}]}""";
        await Assert.That(memoria.JsonCanonico()).IsEqualTo(esperado);

        var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(esperado))).ToLowerInvariant();
        await Assert.That(memoria.Hash()).IsEqualTo("sha256:" + sha256);
    }

    [Test]
    public async Task Mesmas_entradas_dao_o_mesmo_hash()
    {
        await Assert.That(Exemplo().Hash()).IsEqualTo(Exemplo().Hash());
    }

    [Test]
    public async Task Numeros_equivalentes_dao_o_mesmo_hash()
    {
        var comZeros = Exemplo(resultado: 10.000m, tensao: 127.00m);

        await Assert.That(comZeros.Hash()).IsEqualTo(Exemplo().Hash());
    }

    [Test]
    public async Task Texto_em_forma_unicode_decomposta_da_o_mesmo_hash()
    {
        var decomposta = Exemplo(referencia: "FICTÍCIO: seções".Normalize(NormalizationForm.FormD));

        await Assert.That(decomposta.Hash()).IsEqualTo(Exemplo().Hash());
    }

    [Test]
    [Arguments("referencia")]
    [Arguments("expressao")]
    [Arguments("valor")]
    [Arguments("resultado")]
    [Arguments("circuito")]
    [Arguments("perfil")]
    [Arguments("ordem")]
    public async Task Qualquer_mudanca_muda_o_hash(string mudanca)
    {
        var alterada = mudanca switch
        {
            "referencia" => Exemplo(referencia: "TODO_NORMA"),
            "expressao" => Exemplo(expressao: "IB = S / (V · FP)"),
            "valor" => Exemplo(tensao: 127.1m),
            "resultado" => Exemplo(resultado: 10.01m),
            "circuito" => Exemplo(circuito: "TUG-02"),
            "perfil" => Exemplo(perfil: "NBR5410:2004"),
            _ => new MemoriaDeCalculo("TUG-01", "FICTICIO-TESTE", [Passo("B"), Passo("A")])
        };
        var original = mudanca == "ordem" ? new MemoriaDeCalculo("TUG-01", "FICTICIO-TESTE", [Passo("A"), Passo("B")]) : Exemplo();

        await Assert.That(alterada.Hash()).IsNotEqualTo(original.Hash());
    }

    [Test]
    public async Task Passo_sem_referencia_e_rejeitado_porque_toda_linha_cita_a_norma()
    {
        await Assert.That(() => Exemplo(referencia: " "))
            .Throws<MemoriaDeCalculoInvalidaException>()
            .WithMessageContaining("passo 1 sem referência (item da norma ou TODO_NORMA)");
    }

    [Test]
    public async Task Memoria_sem_passos_e_rejeitada()
    {
        await Assert.That(() => new MemoriaDeCalculo("TUG-01", "FICTICIO-TESTE", []))
            .Throws<MemoriaDeCalculoInvalidaException>()
            .WithMessageContaining("memória sem passos");
    }

    [Test]
    public async Task Nome_de_valor_repetido_no_passo_e_rejeitado()
    {
        var passo = new PassoDeCalculo("TODO_NORMA", "Teste", "X = A + A", [new ValorDoPasso("A", 1m, "V"), new ValorDoPasso("A", 2m, "V")], 3m, "V");

        await Assert.That(() => new MemoriaDeCalculo("TUG-01", "FICTICIO-TESTE", [passo]))
            .Throws<MemoriaDeCalculoInvalidaException>()
            .WithMessageContaining("passo 1: valor 'A' repetido");
    }

    [Test]
    public async Task Passo_de_ausencia_registra_resultado_nulo_e_observacao()
    {
        var ausencia = new PassoDeCalculo("TODO_NORMA", "Capacidade de condução", "IZ = tabela(método, seção)", [], null, "A",
            "tabela capacidade_de_conducao_a sem dados oficiais (TODO_NORMA)");

        var json = new MemoriaDeCalculo("TUG-01", "NBR5410:2004", [ausencia]).JsonCanonico();

        await Assert.That(json).Contains("\"resultado\":null");
        await Assert.That(json).Contains("\"obs\":\"tabela capacidade_de_conducao_a sem dados oficiais (TODO_NORMA)\"");
    }

    private static MemoriaDeCalculo Exemplo(
        string referencia = "FICTÍCIO: seções",
        string expressao = "IB = S / V",
        decimal tensao = 127m,
        decimal resultado = 10m,
        string circuito = "TUG-01",
        string perfil = "FICTICIO-TESTE") =>
        new(circuito, perfil,
        [
            new PassoDeCalculo(referencia, "Corrente de projeto", expressao,
                [new ValorDoPasso("S", 1270m, "VA"), new ValorDoPasso("V", tensao, "V")], resultado, "A")
        ]);

    private static PassoDeCalculo Passo(string nome) => new("TODO_NORMA", nome, $"{nome} = 1", [], 1m, "A");
}
