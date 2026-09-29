namespace Ampere.Core.Parametros;

/// <summary>
///     Categorias de elementos às quais os parâmetros Ampere podem ser vinculados.
///     O adapter Revit traduz cada uma para a categoria nativa correspondente.
/// </summary>
public enum CategoriaEletrica
{
    /// <summary>Luminárias.</summary>
    Luminarias,

    /// <summary>Dispositivos de iluminação (interruptores, sensores).</summary>
    DispositivosDeIluminacao,

    /// <summary>Dispositivos elétricos (tomadas).</summary>
    DispositivosEletricos,

    /// <summary>Equipamentos mecânicos (ar-condicionado, motores).</summary>
    EquipamentosMecanicos,

    /// <summary>Equipamentos elétricos (quadros).</summary>
    EquipamentosEletricos,

    /// <summary>Circuitos elétricos.</summary>
    CircuitosEletricos
}
