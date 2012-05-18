using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Microsoft.AnalysisServices;

namespace DaxStudio
{
    public partial class ServerManager : Form
    {
        ServerConnections _connections;

        public ServerManager(ServerConnections Connections)
        {
            InitializeComponent();
            _connections = Connections;
        }

        /****************************************************************************
         * Potential Issues
         * PTB - pressing retrun while focus is in the server casuses a server call
         *
         ****************************************************************************/

        private void ListModels()
        {
            /* Load models available on the server */

            cmboModels.Items.Clear();

            try
            {
                Cursor.Current = Cursors.WaitCursor;
                Server _s = new Server();
                _s.Connect(cmboServer.Text);

                foreach (Database _d in _s.Databases)
                {
                    cmboModels.Items.Add(_d.Name);
                }

                if (cmboModels.Items.Count >= 1)
                    cmboModels.SelectedIndex = 0;

                _s.Disconnect();
                Cursor.Current = Cursors.Default;

            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "DAX Studio", MessageBoxButtons.OK);
                Cursor.Current = Cursors.Default;

            }

        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cmboServer_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void cmboServer_TextUpdate(object sender, EventArgs e)
        {

        }

        private void cmboServer_Leave(object sender, EventArgs e)
        {
            ListModels();
        }

        private void cmdAddModel_Click(object sender, EventArgs e)
        {
            string _model_name = cmboModels.Text;
            string _conString = "Data Source=" + cmboServer.Text + ";" + "Initial Catalog=" + cmboModels.Text + ";";
            string _server = cmboServer.Text;

            // check to see if the model exists in library
            // if so prompt for overwrite
            if (_connections.IndexOf(_model_name) == -1)
            {
                _connections.AddConnection(_server, _model_name, _conString);
            }
            else
            {
                if (MessageBox.Show("Model Exists, Over Write ?", "DAX Studio", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                    _connections.AddConnection(_server, _model_name, _conString);
            }

        }

        private void cmboModels_SelectedIndexChanged(object sender, EventArgs e)
        {

        }













    }
}
