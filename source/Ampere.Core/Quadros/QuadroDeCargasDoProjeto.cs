using Ampere.Core.Cargas;
using Ampere.Core.Normas;

namespace Ampere.Core.Quadros;

/// <summary>Circuito lido do documento, ainda sem validação.</summary>
/// <param name="Id">Identificador do circuito no documento (é por ele que os resultados voltam).</param>
/// <param name="Numero">AMP_NumeroCircuito do sistema (ou o nome nativo).</param>
/// <param name="Tipo">AMP_TipoCarga do sistema (código), se gravado.</param>
/// <param name="PotenciaVA">Soma de AMP_PotenciaInstaladaVA dos membros, se houver.</param>
/// <param name="Fases">AMP_Fases do primeiro membro que tiver (ex.: "F+N").</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV do primeiro membro que tiver.</param>
public sealed record CircuitoLido(long Id, string? Numero, string? Tipo, decimal? PotenciaVA, string? Fases, decimal? TensaoV);

/// <summary>Quadro do documento com os seus circuitos lidos.</summary>
public sealed record QuadroLido(long Id, string Nome, IReadOnlyList<CircuitoLido> Circuitos);

/// <summary>Resultado do quadro de cargas de um quadro do documento.</summary>
/// <param name="CircuitosDasLinhas">Id do circuito de cada linha de <see cref="ResultadoDoQuadroDeCargas.Linhas" />, na mesma ordem.</param>
/// <param name="ForaDoQuadro">Circuitos lidos que ficaram fora do quadro (sem tipo ou sem potência), com o motivo nos problemas.</param>
public sealed record ResultadoDoQuadro(
    long Id,
    string Nome,
    ResultadoDoQuadroDeCargas Quadro,
    IReadOnlyList<long> CircuitosDasLinhas,
    IReadOnlyList<CircuitoLido> ForaDoQuadro);

/// <summary>
///     Uma linha a gravar num circuito: potência instalada e fator aplicado. Nulo apaga o valor anterior — circuito fora
///     do quadro não fica com o fator da montagem passada.
/// </summary>
public sealed record LinhaParaGravar(long CircuitoId, string NumeroDoCircuito, decimal? PotenciaVA, decimal? Fator);

/// <summary>O que a gravação fez: circuitos atualizados e quadros cujo hash o Revit não deixou gravar (grupo ou vínculo).</summary>
public sealed record GravacaoDosQuadros(int CircuitosAtualizados, IReadOnlyList<string> QuadrosSemMemoria);

/// <summary>
///     Porta para os quadros e circuitos de um documento no caso de uso do quadro de cargas (implementada pelo
///     adapter Revit).
/// </summary>
public interface IDocumentoDeQuadros : IDocumentoTransacional
{
    /// <summary>Quadros com circuitos atribuídos; quadro sem circuito fica de fora.</summary>
    IReadOnlyList<QuadroLido> LerQuadrosComCircuitos();

    /// <summary>Grava potência e fator nos circuitos; qualquer falha aborta a transação inteira.</summary>
    void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas);

    /// <summary>
    ///     Grava no próprio quadro o hash da memória do quadro de cargas (AMP_MemoriaCalculoId do quadro; o do circuito é
    ///     da memória do dimensionamento). Nulo apaga o anterior. Devolve <c>false</c>, sem gravar, se o quadro não aceita
    ///     edição (em grupo ou vínculo) — o resto da montagem segue.
    /// </summary>
    bool GravarMemoriaDoQuadro(long quadroId, string? hashDaMemoria);

    /// <summary>Apaga o hash da memória de quadro de cargas dos quadros que não estão entre os montados (ficaram sem circuitos).</summary>
    void ApagarMemoriaDosOutrosQuadros(IReadOnlyCollection<long> montados);

    /// <summary>Cria (substituindo se já existir) a tabela do quadro de cargas; devolve o nome da view criada.</summary>
    string CriarTabelaDoQuadro(string nomeDoQuadro);
}

/// <summary>
///     Caso de uso "Montar quadros de cargas" (especificação §3, F1.4): lê os quadros, valida os circuitos e monta
///     cada quadro contra o perfil. Não grava nada — o resultado é para exibir e para os relatórios.
/// </summary>
/// <remarks>
///     Esquema e tensão do quadro vêm dos próprios circuitos (AMP_Fases e AMP_TensaoCircuitoV uniformes): mistos ou
///     ausentes deixam a corrente sem cálculo, com o motivo nos problemas — o motor não escolhe por conta própria.
/// </remarks>
public static class QuadroDeCargasDoProjeto
{
    /// <summary>Nome da transação de gravação, que aparece no menu Desfazer do Revit.</summary>
    public const string NomeDaTransacao = "Ampere: montar quadro de cargas";
    public static IReadOnlyList<ResultadoDoQuadro> Executar(
        IDocumentoDeQuadros documento,
        PerfilNormativo perfil,
        IReadOnlyDictionary<TipoDeCarga, decimal>? fatoresInformados = null)
    {
        var resultados = new List<ResultadoDoQuadro>();
        foreach (var quadro in documento.LerQuadrosComCircuitos())
        {
            var problemas = new List<string>();
            var circuitos = new List<CircuitoDoQuadro>();
            var idsDasLinhas = new List<long>();
            var foraDoQuadro = new List<CircuitoLido>();
            foreach (var lido in quadro.Circuitos)
            {
                var numero = string.IsNullOrWhiteSpace(lido.Numero) ? "(sem número)" : lido.Numero.Trim();
                if (!CodigosDeTipoDeCarga.TryLer(lido.Tipo, out var tipo))
                {
                    problemas.Add($"circuito {numero}: sem AMP_TipoCarga reconhecido (crie os circuitos com o Ampere ou classifique-os)");
                    foraDoQuadro.Add(lido);
                    continue;
                }

                if (lido.PotenciaVA is not { } potencia)
                {
                    problemas.Add($"circuito {numero}: sem potência instalada (classifique os pontos com AMP_PotenciaInstaladaVA)");
                    foraDoQuadro.Add(lido);
                    continue;
                }

                circuitos.Add(new CircuitoDoQuadro(numero, null, tipo, potencia));
                idsDasLinhas.Add(lido.Id);
            }

            // O par (esquema, tensão) do quadro precisa ser único entre os circuitos que o informam.
            var pares = quadro.Circuitos
                .Where(lido => lido.Fases is { Length: > 0 } && lido.TensaoV is > 0)
                .Select(lido => (lido.Fases!.Trim(), lido.TensaoV!.Value))
                .Distinct()
                .ToList();
            string esquema;
            decimal tensao;
            switch (pares.Count)
            {
                case 1:
                    (esquema, tensao) = pares[0];
                    break;
                case 0:
                    esquema = "F+N";
                    tensao = 0m;
                    problemas.Add("nenhum circuito com AMP_Fases e AMP_TensaoCircuitoV: corrente do quadro não calculada");
                    break;
                default:
                    esquema = "F+N";
                    tensao = 0m;
                    problemas.Add($"circuitos com esquemas/tensões diferentes ({string.Join(", ", pares.Select(par => $"{par.Item1} {NumeroEmTexto.Formatar(par.Item2)} V"))}): corrente do quadro não calculada");
                    break;
            }

            // Montar devolve uma linha por circuito, na ordem recebida: é assim que os ids acompanham as linhas.
            var montado = QuadroDeCargas.Montar(quadro.Nome, esquema, tensao, circuitos, perfil, fatoresInformados);
            if (problemas.Count > 0) montado = montado with { Problemas = [.. montado.Problemas, .. problemas] };
            resultados.Add(new ResultadoDoQuadro(quadro.Id, quadro.Nome, montado, idsDasLinhas, foraDoQuadro));
        }

        return resultados;
    }

    /// <summary>
    ///     Grava os resultados numa única transação — um único desfazer: em cada circuito, a potência instalada e o fator
    ///     aplicado; em cada quadro, o hash da memória do quadro (é ela que justifica os fatores). Nada da montagem anterior
    ///     sobrevive com cara de atual: linha sem fator apaga o fator anterior; circuito fora do quadro fica com a potência
    ///     lida (ou nenhuma) e sem fator; quadro incompleto, ou que ficou sem circuitos, perde o hash. Quadro em grupo ou
    ///     vínculo fica sem o hash e é informado; qualquer outra recusa do Revit aborta tudo.
    /// </summary>
    public static GravacaoDosQuadros Gravar(IReadOnlyList<ResultadoDoQuadro> resultados, IDocumentoDeQuadros documento)
    {
        var linhas = resultados
            .SelectMany(resultado => resultado.Quadro.Linhas
                .Select((linha, indice) => new LinhaParaGravar(resultado.CircuitosDasLinhas[indice], linha.Numero, linha.PotenciaInstaladaVA, linha.Fator))
                .Concat(resultado.ForaDoQuadro.Select(lido => new LinhaParaGravar(lido.Id, lido.Numero ?? "(sem número)", lido.PotenciaVA, null))))
            .ToList();
        if (resultados.Count == 0) return new GravacaoDosQuadros(0, []);

        var semMemoria = new List<string>();
        documento.EmUmaTransacao(NomeDaTransacao, () =>
        {
            if (linhas.Count > 0) documento.GravarLinhas(linhas);
            foreach (var resultado in resultados)
            {
                if (!documento.GravarMemoriaDoQuadro(resultado.Id, resultado.Quadro.Memoria?.Hash())) semMemoria.Add(resultado.Nome);
            }

            documento.ApagarMemoriaDosOutrosQuadros(resultados.Select(resultado => resultado.Id).ToList());
        });
        return new GravacaoDosQuadros(linhas.Count, semMemoria);
    }

    /// <summary>
    ///     Cria a tabela de cada quadro numa única transação: os circuitos com os AMP_* gravados, como view nativa do
    ///     Revit (aparece no navegador de projeto, pode ir para prancha). Tabela já existente é recriada — o comando é
    ///     idempotente.
    /// </summary>
    public static IReadOnlyList<string> CriarTabelas(IReadOnlyList<ResultadoDoQuadro> resultados, IDocumentoDeQuadros documento)
    {
        var nomes = new List<string>();
        documento.EmUmaTransacao(NomeDaTransacao, () => nomes.AddRange(resultados.Select(resultado => documento.CriarTabelaDoQuadro(resultado.Nome))));
        return nomes;
    }
}
