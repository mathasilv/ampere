namespace Ampere.Core.Normas;

/// <summary>
///     Regras da norma usadas pelo motor (não são tabelas, mas precisam de referência igual). A referência de cada uma
///     fica no perfil, em "regras".
/// </summary>
public enum RegraNormativa
{
    /// <summary>Definição e cálculo da corrente de projeto I<sub>B</sub> ("corrente_de_projeto").</summary>
    CorrenteDeProjeto,

    /// <summary>Coordenação entre condutor e proteção, I<sub>B</sub> ≤ I<sub>n</sub> ≤ I<sub>Z</sub> ("coordenacao_condutor_protecao").</summary>
    CoordenacaoCondutorProtecao,

    /// <summary>Critério de queda de tensão ("queda_de_tensao").</summary>
    QuedaDeTensao,

    /// <summary>Condutores de um circuito dentro do eletroduto: fases, neutro e proteção ("condutores_no_eletroduto").</summary>
    CondutoresNoEletroduto,

    /// <summary>Corrente nominal do IDR coordenada com o disjuntor do circuito ("coordenacao_idr_disjuntor").</summary>
    CoordenacaoIdrDisjuntor
}
