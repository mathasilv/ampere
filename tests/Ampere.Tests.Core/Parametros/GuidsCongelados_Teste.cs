using Ampere.Core.Parametros;

namespace Ampere.Tests.Core.Parametros;

/// <summary>
///     Regra inviolável nº 2: os GUIDs dos parâmetros AMP_* são congelados.
/// </summary>
/// <remarks>
///     Os literais abaixo são a tabela da especificação do MVP (§5), fonte independente do JSON embarcado — os
///     dois precisam concordar. Nunca altere um GUID aqui para "fazer o teste passar": GUID novo exige decisão
///     registrada no catálogo versionado.
/// </remarks>
public class GuidsCongelados_Teste
{
    private static readonly (string Nome, string Guid, string Tipo)[] Especificacao =
    [
        ("AMP_NumeroCircuito", "952b3b09-e30f-46b7-a2e0-41da05638dff", "TEXT"),
        ("AMP_TipoCarga", "de59d3d9-f2e4-48ad-8f01-37eac54aeb24", "TEXT"),
        ("AMP_TensaoCircuitoV", "400891f8-9e33-4169-8f96-ef1d7cb9f8a9", "NUMBER"),
        ("AMP_Fases", "df7575ba-4cf2-401b-a8f6-7e2ea38789dd", "TEXT"),
        ("AMP_PotenciaInstaladaVA", "271e3ca9-095e-4faa-990a-bf29b6ef0c96", "ELECTRICAL_APPARENT_POWER"),
        ("AMP_FatorDemanda", "5774436a-ca44-474f-8029-02ad32f3afcc", "NUMBER"),
        ("AMP_FatorPotencia", "d0cb1ab2-e4bf-4503-aee2-c454de483907", "NUMBER"),
        ("AMP_CorrenteProjetoA", "3351b48f-8ad5-4b84-b038-44725ee1fefc", "CURRENT"),
        ("AMP_MetodoInstalacao", "16687dae-c522-43d8-b894-590cb37c356c", "TEXT"),
        ("AMP_MaterialIsolacao", "ec7ea6ab-ecee-496c-8bef-6a4e4ca02aa5", "TEXT"),
        ("AMP_TipoCondutor", "3ee0a581-296c-4159-af3d-7543d7f4473c", "TEXT"),
        ("AMP_BitolaCondutorMm2", "23ce7195-2a0e-4b0d-af1f-8c190c10172e", "NUMBER"),
        ("AMP_CapacidadeConducaoA", "3854720d-d995-4791-b374-3844b99eeced", "CURRENT"),
        ("AMP_FCA", "7040703c-6018-4b88-8a70-628f5aca4dbd", "NUMBER"),
        ("AMP_FCT", "d91016fc-cf2c-4b16-812a-b5801ad2d14a", "NUMBER"),
        ("AMP_DisjuntorNominalA", "25c19efa-5ca1-40b1-9ba6-bbd66a2d1899", "NUMBER"),
        ("AMP_CurvaDisjuntor", "39e9361c-d39e-468f-87e1-ee575e42e990", "TEXT"),
        ("AMP_IDR_NominalA", "70b4a070-b890-4743-9588-868f9ee9c3e5", "NUMBER"),
        ("AMP_IDR_SensibilidadeMa", "a8ed44a7-0de2-4116-907b-bbb60d268c0f", "NUMBER"),
        ("AMP_QuedaTensaoPct", "5f98123c-3b4e-4a90-96b3-8742f242ddef", "NUMBER"),
        ("AMP_ComprimentoRotaM", "6bd40602-4daf-4c95-bf48-8e426b563bf1", "LENGTH"),
        ("AMP_OcupacaoEletrodutoPct", "3b1b037f-efcc-4a3b-9be6-e5dc239d0178", "NUMBER"),
        ("AMP_EletrodutoTipo", "d494ec29-87fa-4e2d-9550-80f4a6e3cc62", "TEXT"),
        ("AMP_Quadro", "9e1989ab-5dbe-44a3-bad2-34aac6951d6f", "TEXT"),
        ("AMP_PerfilNorma", "3847aabf-9378-4b04-a5dc-6fec34742c21", "TEXT"),
        ("AMP_MemoriaCalculoId", "9c91f208-216c-4f76-a56c-d549fbd6d423", "TEXT")
    ];

    private static readonly Dictionary<string, TipoDeDadoDoParametro> TipoNaEspecificacao = new()
    {
        ["TEXT"] = TipoDeDadoDoParametro.Texto,
        ["NUMBER"] = TipoDeDadoDoParametro.Numero,
        ["ELECTRICAL_APPARENT_POWER"] = TipoDeDadoDoParametro.PotenciaAparente,
        ["CURRENT"] = TipoDeDadoDoParametro.Corrente,
        ["LENGTH"] = TipoDeDadoDoParametro.Comprimento
    };

    [Test]
    public async Task Catalogo_embarcado_tem_exatamente_os_parametros_da_especificacao()
    {
        var esperado = Especificacao
            .Select(parametro => (parametro.Nome, Guid.Parse(parametro.Guid), TipoNaEspecificacao[parametro.Tipo]))
            .ToArray();
        var atual = CatalogoDeParametros.Padrao.Parametros
            .Select(parametro => (parametro.Nome, parametro.Guid, parametro.Tipo))
            .ToArray();

        await Assert.That(atual.Except(esperado)).IsEmpty();
        await Assert.That(esperado.Except(atual)).IsEmpty();
        await Assert.That(atual.Length).IsEqualTo(Especificacao.Length);
    }
}
