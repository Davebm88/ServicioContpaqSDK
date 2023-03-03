using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ServicioContpaqSDK
{
    partial class ContpaqSDK : ServiceBase
    {
        bool blBandera = false;

        public ContpaqSDK()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            stLapso.Start();
        }

        protected override void OnStop()
        {
            stLapso.Stop();
        }

        private void stLapso_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (blBandera) return;
            
            blBandera = true;
            bool nuevos = false;

            using (TaguesiDataContext db = new TaguesiDataContext())
            {
                if (db.Polizas.Where(x => x.Registrar || x.Modificar || x.Cancelar || x.Eliminar).ToList().Count() > 0)
                    nuevos = true;
            
                if (nuevos)
                {
                    SaveLog_Polizas("Se inicio proceso de creacion de polizas Contpaq", "Mensaje", "Inicio Proceso");

                    SDKCONTPAQNGLib.TSdkSesion sesion = new SDKCONTPAQNGLib.TSdkSesion();

                    if (sesion.conexionActiva == 0)
                    {
                        sesion.iniciaConexion();
                    }
                    if (sesion.conexionActiva == 1 && sesion.ingresoUsuario == 0)
                    {
                        sesion.firmaUsuarioParams("SUPERVISOR", "sistemas");
                        sesion.firmaUsuario();
                    }

                    #region Registrar
                    Registrar(sesion);
                    #endregion

                    #region Modificar
                    Modificar(sesion);
                    #endregion

                    #region Cancelar
                    Cancelar(sesion);
                    #endregion

                    #region Eliminar
                    Eliminar(sesion);
                    #endregion

                    if (sesion.conexionActiva == 1)
                    {
                        sesion.finalizaConexion();
                        TerminarProcesoSDK();
                    }
                }
            }

            blBandera = false;
        }

        private void Registrar(SDKCONTPAQNGLib.TSdkSesion sesion)
        {
            using (TaguesiDataContext db = new TaguesiDataContext())
            {
                foreach (var pol in db.Polizas.Where(x => x.Registrar))
                {
                    //SDK Contpaq
                    SDKCONTPAQNGLib.TSdkPoliza poliza = new SDKCONTPAQNGLib.TSdkPoliza();
                    SDKCONTPAQNGLib.TSdkTipoPoliza tpoliza = new SDKCONTPAQNGLib.TSdkTipoPoliza();
                    SDKCONTPAQNGLib.TSdkMovimientoPoliza movimientosPoliza = new SDKCONTPAQNGLib.TSdkMovimientoPoliza();
                    SDKCONTPAQNGLib.TSdkCuenta cuenta = new SDKCONTPAQNGLib.TSdkCuenta();
                    SDKCONTPAQNGLib.TSdkEmpresa empresa = new SDKCONTPAQNGLib.TSdkEmpresa();
                    
                    if (sesion.conexionActiva == 1 && sesion.ingresoUsuario == 1)
                    {
                        var empContpaq = db.PolizasAdmin.Where(x => x.Tipo == pol.TipoDocumento).Select(x => x.Empresa).FirstOrDefault();
                        if (!string.IsNullOrEmpty(empContpaq))
                            sesion.abreEmpresa(empContpaq);
                    }

                    empresa.setSesion(sesion);
                    poliza.setSesion(sesion);
                    cuenta.setSesion(sesion);
                    tpoliza.setSesion(sesion);
                    movimientosPoliza.setSesion(sesion);

                    try
                    {
                        int idEmpresa = empresa.IdEmpresa;

                        if (idEmpresa > 0)
                        {
                            poliza.iniciarInfo();
                            tpoliza.iniciarInfo();
                            tpoliza.Tipo = (SDKCONTPAQNGLib.ETIPOPOLIZA)(int.Parse(pol.TipoPoliza));
                            poliza.Fecha = pol.Fecha ?? DateTime.Today;
                            poliza.Tipo = tpoliza.Tipo;
                            poliza.Numero = poliza.getUltimoNumero(poliza.Ejercicio, poliza.Periodo, tpoliza.Tipo);
                            poliza.Clase = SDKCONTPAQNGLib.ECLASEPOLIZA.CLASE_AFECTAR;
                            poliza.Impresa = 0;
                            poliza.Concepto = pol.Concepto;
                            poliza.SistOrigen = SDKCONTPAQNGLib.ESISTORIGEN.ORIG_CONTPAQNG;

                            foreach (var mov in pol.PolizasDetalle)
                            {
                                movimientosPoliza.iniciarInfo();
                                movimientosPoliza.NumMovto = mov.NumMovto;
                                movimientosPoliza.CodigoCuenta = mov.CuentaContpaq;
                                movimientosPoliza.TipoMovto = ((bool)mov.TipoMovto) ? SDKCONTPAQNGLib.ETIPOIMPORTEMOVPOLIZA.MOVPOLIZA_ABONO : SDKCONTPAQNGLib.ETIPOIMPORTEMOVPOLIZA.MOVPOLIZA_CARGO;
                                movimientosPoliza.Importe = Convert.ToDecimal(mov.Importe ?? 0);
                                movimientosPoliza.ImporteME = Convert.ToDecimal(mov.ImporteME ?? 0);
                                movimientosPoliza.Concepto = mov.Concepto;
                                movimientosPoliza.Referencia = mov.Referencia;

                                int movAgregado = poliza.agregaMovimiento(movimientosPoliza);


                                mov.IdMovtoPolizaContpaq = movimientosPoliza.Id;
                            }

                            int grabada = poliza.crea();

                            foreach (var mov in pol.PolizasDetalle)
                            {
                                mov.IdMovtoPolizaContpaq = poliza.buscaMovimientoPorNumMovto(movimientosPoliza, mov.NumMovto);
                            }

                            pol.IdPolizaContpaq = grabada;
                            pol.FolioContpaq = poliza.Numero;
                            pol.Estatus = (int)Enums.Estatus_Poliza.EnContpaq;
                            pol.Registrar = false;

                            db.SubmitChanges();
                            SaveLog_Polizas("Se registro la Poliza #" + poliza.Numero + "\nTipo Poliza: " + pol.TipoPoliza, "Mensaje", "Registrar");
                        }
                    }
                    catch (Exception eContpaq)
                    {
                        SaveLog_Polizas(eContpaq.Message, "Error", "Registrar");
                    }
                    finally
                    {
                        sesion.cierraEmpresa();
                    }
                }
            }
        }

        private void Modificar(SDKCONTPAQNGLib.TSdkSesion sesion)
        {
            using (TaguesiDataContext db = new TaguesiDataContext())
            {
                foreach (var pol in db.Polizas.Where(x => x.Modificar))
                {
                    //SDK Contpaq
                    SDKCONTPAQNGLib.TSdkPoliza poliza = new SDKCONTPAQNGLib.TSdkPoliza();
                    SDKCONTPAQNGLib.TSdkTipoPoliza tpoliza = new SDKCONTPAQNGLib.TSdkTipoPoliza();
                    SDKCONTPAQNGLib.TSdkMovimientoPoliza movimientosPoliza = new SDKCONTPAQNGLib.TSdkMovimientoPoliza();
                    SDKCONTPAQNGLib.TSdkCuenta cuenta = new SDKCONTPAQNGLib.TSdkCuenta();
                    SDKCONTPAQNGLib.TSdkEmpresa empresa = new SDKCONTPAQNGLib.TSdkEmpresa();

                    if (sesion.conexionActiva == 1 && sesion.ingresoUsuario == 1)
                    {
                        var empContpaq = db.PolizasAdmin.Where(x => x.Tipo == pol.TipoDocumento).Select(x => x.Empresa).FirstOrDefault();
                        if (!string.IsNullOrEmpty(empContpaq))
                            sesion.abreEmpresa(empContpaq);
                    }

                    empresa.setSesion(sesion);
                    poliza.setSesion(sesion);
                    cuenta.setSesion(sesion);
                    tpoliza.setSesion(sesion);
                    movimientosPoliza.setSesion(sesion);

                    try
                    {
                        int idEmpresa = empresa.IdEmpresa;
                        if (idEmpresa > 0)
                        {
                            poliza.iniciarInfo();
                            tpoliza.iniciarInfo();
                            tpoliza.Tipo = (SDKCONTPAQNGLib.ETIPOPOLIZA)(int.Parse(pol.TipoPoliza));

                            poliza.buscaPorId(pol.IdPolizaContpaq ?? 0);

                            poliza.Fecha = pol.Fecha ?? poliza.Fecha;
                            poliza.Tipo = tpoliza.Tipo;
                            poliza.Clase = SDKCONTPAQNGLib.ECLASEPOLIZA.CLASE_AFECTAR;
                            poliza.Impresa = 0;
                            poliza.Concepto = pol.Concepto;
                            poliza.SistOrigen = SDKCONTPAQNGLib.ESISTORIGEN.ORIG_CONTPAQNG;

                            //Eliminar movimientos actuales
                            int contMov = 1;
                            do
                            {
                                poliza.buscaMovimientoPorNumMovto(movimientosPoliza, contMov);
                                if (movimientosPoliza.Id > 0)
                                {
                                    poliza.borraMovimiento(movimientosPoliza);
                                }
                            }
                            while (movimientosPoliza.Id > 0);

                            contMov = 1;
                            //Re creamos movimientos
                            foreach (var mov in pol.PolizasDetalle)
                            {
                                movimientosPoliza.iniciarInfo();
                                movimientosPoliza.NumMovto = mov.NumMovto;
                                movimientosPoliza.CodigoCuenta = mov.CuentaContpaq;
                                movimientosPoliza.TipoMovto = ((bool)mov.TipoMovto) ? SDKCONTPAQNGLib.ETIPOIMPORTEMOVPOLIZA.MOVPOLIZA_ABONO : SDKCONTPAQNGLib.ETIPOIMPORTEMOVPOLIZA.MOVPOLIZA_CARGO;
                                movimientosPoliza.Importe = Convert.ToDecimal(mov.Importe ?? 0);
                                movimientosPoliza.ImporteME = Convert.ToDecimal(mov.ImporteME ?? 0);
                                movimientosPoliza.Concepto = mov.Concepto;
                                movimientosPoliza.Referencia = mov.Referencia;

                                poliza.creaMovimiento(movimientosPoliza);
                                contMov++;
                            }

                            int modificada = poliza.modifica();

                            pol.Modificar = false;

                            db.SubmitChanges();
                            SaveLog_Polizas("Se modifico la Poliza #" + poliza.Numero + "\nTipo Poliza: " + pol.TipoPoliza, "Mensaje", "Modificar");
                        }
                    }
                    catch (Exception eContpaq)
                    {
                        SaveLog_Polizas(eContpaq.Message, "Error", "Modificar");
                    }
                    finally
                    {
                        sesion.cierraEmpresa();
                    }
                }
            }
        }

        private void Cancelar(SDKCONTPAQNGLib.TSdkSesion sesion)
        {
            using (TaguesiDataContext db = new TaguesiDataContext())
            {
                foreach (var pol in db.Polizas.Where(x => x.Cancelar))
                {
                    //SDK Contpaq
                    SDKCONTPAQNGLib.TSdkPoliza poliza = new SDKCONTPAQNGLib.TSdkPoliza();
                    SDKCONTPAQNGLib.TSdkTipoPoliza tpoliza = new SDKCONTPAQNGLib.TSdkTipoPoliza();
                    SDKCONTPAQNGLib.TSdkMovimientoPoliza movimientosPoliza = new SDKCONTPAQNGLib.TSdkMovimientoPoliza();
                    SDKCONTPAQNGLib.TSdkCuenta cuenta = new SDKCONTPAQNGLib.TSdkCuenta();
                    SDKCONTPAQNGLib.TSdkEmpresa empresa = new SDKCONTPAQNGLib.TSdkEmpresa();

                    if (sesion.conexionActiva == 1 && sesion.ingresoUsuario == 1)
                    {
                        var empContpaq = db.PolizasAdmin.Where(x => x.Tipo == pol.TipoDocumento).Select(x => x.Empresa).FirstOrDefault();
                        if (!string.IsNullOrEmpty(empContpaq))
                            sesion.abreEmpresa(empContpaq);
                    }

                    empresa.setSesion(sesion);
                    poliza.setSesion(sesion);
                    cuenta.setSesion(sesion);
                    tpoliza.setSesion(sesion);
                    movimientosPoliza.setSesion(sesion);

                    try
                    {
                        int idEmpresa = empresa.IdEmpresa;
                        if (idEmpresa > 0)
                        {
                            poliza.iniciarInfo();
                            tpoliza.iniciarInfo();
                            tpoliza.Tipo = (SDKCONTPAQNGLib.ETIPOPOLIZA)(int.Parse(pol.TipoPoliza));

                            poliza.buscaPorId(pol.IdPolizaContpaq ?? 0);

                            poliza.Fecha = pol.Fecha ?? poliza.Fecha;
                            poliza.Tipo = tpoliza.Tipo;
                            poliza.Clase = SDKCONTPAQNGLib.ECLASEPOLIZA.CLASE_AFECTAR;
                            poliza.Impresa = 0;
                            poliza.Concepto = "CANCELADA";
                            poliza.SistOrigen = SDKCONTPAQNGLib.ESISTORIGEN.ORIG_CONTPAQNG;

                            int modificada = poliza.modifica();

                            //Eliminar movimientos actuales
                            int contMov = 1;
                            do
                            {
                                poliza.buscaMovimientoPorNumMovto(movimientosPoliza, contMov);
                                if (movimientosPoliza.Id > 0)
                                {
                                    poliza.borraMovimiento(movimientosPoliza);
                                }
                            }
                            while (movimientosPoliza.Id > 0);

                            contMov = 1;
                            //Re creamos movimientos
                            foreach (var mov in pol.PolizasDetalle)
                            {
                                movimientosPoliza.iniciarInfo();
                                movimientosPoliza.NumMovto = mov.NumMovto;
                                movimientosPoliza.CodigoCuenta = mov.CuentaContpaq;
                                movimientosPoliza.TipoMovto = ((bool)mov.TipoMovto) ? SDKCONTPAQNGLib.ETIPOIMPORTEMOVPOLIZA.MOVPOLIZA_ABONO : SDKCONTPAQNGLib.ETIPOIMPORTEMOVPOLIZA.MOVPOLIZA_CARGO;
                                movimientosPoliza.Importe = 0;
                                movimientosPoliza.ImporteME = 0;
                                movimientosPoliza.Concepto = mov.Concepto;
                                movimientosPoliza.Referencia = mov.Referencia;

                                poliza.creaMovimiento(movimientosPoliza);
                                contMov++;
                            }

                            pol.Cancelar = false;
                            pol.Estatus = (int)Enums.Estatus_Poliza.Cancelada;

                            db.SubmitChanges();
                            SaveLog_Polizas("Se cancelo la Poliza #" + poliza.Numero + "\nTipo Poliza: " + pol.TipoPoliza, "Mensaje", "Cancelar");
                        }
                    }
                    catch (Exception eContpaq)
                    {
                        SaveLog_Polizas(eContpaq.Message, "Error", "Cancelar");
                    }
                    finally
                    {
                        sesion.cierraEmpresa();
                    }
                }
            }
        }

        private void Eliminar(SDKCONTPAQNGLib.TSdkSesion sesion)
        {
            using (TaguesiDataContext db = new TaguesiDataContext())
            {
                foreach (var pol in db.Polizas.Where(x => x.Eliminar))
                {
                    //SDK Contpaq
                    SDKCONTPAQNGLib.TSdkPoliza poliza = new SDKCONTPAQNGLib.TSdkPoliza();
                    SDKCONTPAQNGLib.TSdkTipoPoliza tpoliza = new SDKCONTPAQNGLib.TSdkTipoPoliza();
                    SDKCONTPAQNGLib.TSdkMovimientoPoliza movimientosPoliza = new SDKCONTPAQNGLib.TSdkMovimientoPoliza();
                    SDKCONTPAQNGLib.TSdkCuenta cuenta = new SDKCONTPAQNGLib.TSdkCuenta();
                    SDKCONTPAQNGLib.TSdkEmpresa empresa = new SDKCONTPAQNGLib.TSdkEmpresa();

                    if (sesion.conexionActiva == 1 && sesion.ingresoUsuario == 1)
                    {
                        var empContpaq = db.PolizasAdmin.Where(x => x.Tipo == pol.TipoDocumento).Select(x => x.Empresa).FirstOrDefault();
                        if (!string.IsNullOrEmpty(empContpaq))
                            sesion.abreEmpresa(empContpaq);
                    }

                    empresa.setSesion(sesion);
                    poliza.setSesion(sesion);
                    cuenta.setSesion(sesion);
                    tpoliza.setSesion(sesion);
                    movimientosPoliza.setSesion(sesion);

                    try
                    {
                        int idEmpresa = empresa.IdEmpresa;
                        if (idEmpresa > 0)
                        {
                            poliza.iniciarInfo();
                            tpoliza.iniciarInfo();
                            tpoliza.Tipo = (SDKCONTPAQNGLib.ETIPOPOLIZA)(int.Parse(pol.TipoPoliza));

                            poliza.buscaPorId(pol.IdPolizaContpaq ?? 0);

                            if ((poliza.Numero + 1) == poliza.getUltimoNumero(poliza.Ejercicio, poliza.Periodo, tpoliza.Tipo))
                            {
                                //BORRAMOS Si es el ultimo folio de poliza registrado hasta el momento
                                poliza.borra();

                                //Borramos la poliza del sistema ERP Taguesi
                                var polElim = (from e in db.GetTable<Polizas>()
                                               where e.Id == pol.Id
                                               select e).Single<Polizas>();

                                foreach (PolizasDetalle pd in pol.PolizasDetalle)
                                {
                                    db.GetTable<PolizasDetalle>().DeleteOnSubmit(pd);
                                }
                                db.GetTable<Polizas>().DeleteOnSubmit(pol);
                            }
                            else
                            {
                                poliza.Fecha = pol.Fecha ?? poliza.Fecha;
                                poliza.Tipo = tpoliza.Tipo;
                                poliza.Clase = SDKCONTPAQNGLib.ECLASEPOLIZA.CLASE_AFECTAR;
                                poliza.Impresa = 0;
                                poliza.Concepto = "SIN MOVIMIENTOS";
                                poliza.SistOrigen = SDKCONTPAQNGLib.ESISTORIGEN.ORIG_CONTPAQNG;

                                int modificada = poliza.modifica();

                                //Eliminar movimientos actuales
                                int contMov = 1;
                                do
                                {
                                    poliza.buscaMovimientoPorNumMovto(movimientosPoliza, contMov);
                                    if (movimientosPoliza.Id > 0)
                                    {
                                        poliza.borraMovimiento(movimientosPoliza);
                                    }
                                }
                                while (movimientosPoliza.Id > 0);

                                pol.Eliminar = false;
                                pol.Estatus = (int)Enums.Estatus_Poliza.Cancelada;
                            }

                            db.SubmitChanges();
                            SaveLog_Polizas("Se elimino la Poliza #" + poliza.Numero + "\nTipo Poliza: " + pol.TipoPoliza, "Mensaje", "Eliminar");
                        }
                    }
                    catch (Exception eContpaq)
                    {
                        SaveLog_Polizas(eContpaq.Message, "Error", "Eliminar");
                    }
                    finally
                    {
                        sesion.cierraEmpresa();
                    }
                }
            }
        }

        private void TerminarProcesoSDK()
        {
            var resultado = from item in Process.GetProcesses()
                            where item.ProcessName.ToUpper() == "SDKCONTPAQNG"
                            select item;

            foreach (var item in resultado)
            {
                item.Kill();
            }
        }

        private void SaveLog_Polizas(string Mensaje, string Tipo, string Evento)
        {
            try
            {
                String directory = Path.Combine(@"C:\Resources\ArchivosApp\Logs\");

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                String filename = directory + "LogErrors_Polizas.txt";
                StreamWriter sw = File.AppendText(filename);
                sw.WriteLine("************************************************************************");
                sw.WriteLine("Fecha y Hora: " + DateTime.Now);
                sw.WriteLine("Clase: Servicio Contpaq SDK");
                sw.WriteLine("Evento: " + Evento);
                sw.WriteLine("Tipo: " + Tipo);
                sw.WriteLine("Mensaje: " + Mensaje + "\n\n");
                sw.Close();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ex.Message, EventLogEntryType.Error);
            }
        }
    }
}
