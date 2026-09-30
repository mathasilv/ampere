using Ampere.Core.Cargas;
using Ampere.Core.Normas;

namespace Ampere.Core.Quadros;

/// <summary>Circuito lido do documento, ainda sem validação.</summary>
/// <param name="Numero">AMP_NumeroCircuito do sistema (ou o nome nativo).</param>
/// <param name="Tipo">AMP_TipoCarga do sistema (código), se gravado.</param>
/// <param name="PotenciaVA">Soma de AMP_PotenciaInstaladaVA dos membros, se houver.</param>
/// <param name="Fases">AMP_Fases do primeiro membro que tiver (ex.: "F+N").</param>
/// <param name="TensaoV">AMP_TensaoCircuitoV do primeiro membro que tiver.</param>
public sealed record CircuitoLido(string? Numero, string? Tipo, decimal? PotenciaVA, string? Fases, decimal? TensaoV);

/// <summary>Quadro do documento com os seus circuitos lidos.</summary>
public sealed record QuadroLido(long Id, string Nome, IReadOnlyList<CircuitoLido> Circuitos);

/// <summary>Resultado do quadro de cargas de um quadro do documento.</summary>
public sealed record ResultadoDoQuadro(long Id, string Nome, ResultadoDoQuadroDeCargas Quadro);

/// <summary>Uma linha a gravar no circuito do quadro (potência, fator aplicado e o hash da memória do quadro).</summary>
public sealed record LinhaParaGravar(long QuadroId, string NumeroDoCircuito, decimal PotenciaVA, decimal? Fator, string? HashDaMemoria);

/// <summary>
///     Porta para os quadros e circuitos de um documento no caso de uso do quadro de cargas (implementada pelo
///     adapter Revit).
/// </summary>
public interface IDocumentoDeQuadros : IDocumentoTransacional
{
    /// <summary>Quadros com circuitos atribuídos; quadro sem circuito fica de fora.</summary>
    IReadOnlyList<QuadroLido> LerQuadrosComCircuitos();

    /// <summary>Grava potência, fator e hash da memória nos circuitos; qualquer falha aborta a transação inteira.</summary>
    void GravarLinhas(IReadOnlyList<LinhaParaGravar> linhas);

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
            foreach (var lido in quadro.Circuitos)
            {
                var numero = string.IsNullOrWhiteSpace(lido.Numero) ? "(sem número)" : lido.Numero.Trim();
                if (!CodigosDeTipoDeCarga.TryLer(lido.Tipo, out var tipo))
                {
                    problemas.Add($"circuito {numero}: sem AMP_TipoCarga reconhecido (crie os circuitos com o Ampere ou classifique-os)");
                    continue;
                }

                if (lido.PotenciaVA is not { } potencia)
                {
                    problemas.Add($"circuito {numero}: sem potência instalada (classifique os pontos com AMP_PotenciaInstaladaVA)");
                    continue;
                }

                circuitos.Add(new CircuitoDoQuadro(numero, null, tipo, potencia));
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

            var montado = QuadroDeCargas.Montar(quadro.Nome, esquema, tensao, circuitos, perfil, fatoresInformados);
            if (problemas.Count > 0) montado = montado with { Problemas = [.. montado.Problemas, .. problemas] };
            resultados.Add(new ResultadoDoQuadro(quadro.Id, quadro.Nome, montado));
        }

        return resultados;
    }

    /// <summary>
    ///     Grava os resultados nos circuitos numa única transação — um único desfazer: potência instalada do circuito,
    ///     fator aplicado e o hash da memória do quadro (é ela que justifica o fator). Linha sem fator grava a potência
    ///     e limpa o que havia; qualquer recusa do Revit aborta tudo.
    /// </summary>
    public static int Gravar(IReadOnlyList<ResultadoDoQuadro> resultados, IDocumentoDeQuadros documento)
    {
        var linhas = resultados
            .SelectMany(resultado => resultado.Quadro.Linhas.Select(linha => new LinhaParaGravar(
                resultado.Id, linha.Numero, linha.PotenciaInstaladaVA, linha.Fator, resultado.Quadro.Memoria?.Hash())))
            .ToList();
        if (linhas.Count > 0) documento.EmUmaTransacao(NomeDaTransacao, () => documento.GravarLinhas(linhas));
        return linhas.Count;
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
