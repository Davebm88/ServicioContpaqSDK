namespace ServicioContpaqSDK
{
    partial class ContpaqSDK
    {
        /// <summary> 
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de componentes

        /// <summary> 
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.stLapso = new System.Timers.Timer();
            ((System.ComponentModel.ISupportInitialize)(this.stLapso)).BeginInit();
            // 
            // stLapso
            // 
            this.stLapso.Enabled = true;
            this.stLapso.Interval = 120000D;
            this.stLapso.Elapsed += new System.Timers.ElapsedEventHandler(this.stLapso_Elapsed);
            // 
            // ContpaqSDK
            // 
            this.ServiceName = "ContpaqSDK";
            ((System.ComponentModel.ISupportInitialize)(this.stLapso)).EndInit();

        }

        #endregion

        private System.Timers.Timer stLapso;
    }
}
