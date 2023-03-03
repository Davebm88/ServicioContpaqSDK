using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicioContpaqSDK
{
    public class Enums
    {
        public enum TipoDocumento
        {
            [Description("1")]
            Fiscal = 1,
            [Description("2")]
            Interno
        }

        public enum Estatus_Poliza
        {
            [Description("GENERADA")]
            Generada = 1,
            [Description("EN CONTPAQ")]
            EnContpaq,
            [Description("CANCELADA")]
            Cancelada
        }

        public enum Origen_Poliza
        {
            [Description("COMPRAS PT")]
            ComprasPT = 1,
            [Description("FACTURAS")]
            Facturas,
            [Description("NOTAS DE CREDITO")]
            NotasCredito,
            [Description("PAGOS A CLIENTES")]
            PagosClientes,
            [Description("PAGOS A PROVEEDORES")]
            PagosProveedores,
            [Description("COMPRAS MATERIALES")]
            ComprasMateriales,
            [Description("VENTAS MATERIALES")]
            VentasMateriales
        }
    }
}
