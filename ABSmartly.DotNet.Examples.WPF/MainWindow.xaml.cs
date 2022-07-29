﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using ABSmartly;

namespace WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private ABSmartly.ABSmartly sdk;
        private Dictionary<string, object> properties;
        private Context context;

        private void InitializeABSmartly()
        {
            ClientConfig config = new ClientConfig(
                endpoint: "https://acme.absmartly.io/v1",
                apiKey: "apiKey",
                application: ProductName,
                environment: "Development"
            );

            ABSmartlyConfig sdkConfig = ABSmartlyConfig.Create();

            sdk = ABSmartly.ABSmartly.Create(sdkConfig);

            ContextConfig contextConfig = ContextConfig.Create()
                .SetUnit("session_id", "bf06d8cb5d8137290c4abb64155584fbdb64d8")
                .SetUnit("user_id", "123456");

            context = sdk.CreateContext(contextConfig).WaitUntilReady();
            int treatment = context.GetTreatment("exp_test_ab");
            Debug.WriteLine(treatment);

            properties = new Dictionary<string, object>
            {
                { "value", 125 },
                { "fee", 125 }
            };
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {


            context.Track("payment", properties);


        }


        #region Helper

        public static string ProductName
        {
            get
            {
                AssemblyProductAttribute myProduct =
                    (AssemblyProductAttribute)AssemblyProductAttribute.GetCustomAttribute(Assembly.GetExecutingAssembly(),
                        typeof(AssemblyProductAttribute));
                return myProduct.Product;
            }
        }

        #endregion

        public void Dispose()
        {
            context.Close();
            sdk.Close();            
        }
    }
}