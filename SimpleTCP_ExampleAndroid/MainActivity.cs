using System;
using System.Net;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using SimpleTCP.Android;

namespace SimpleTCP_ExampleAndroid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;
            var button = FindViewById(Resource.Id.button1);
            Test();

            button.Click += MainActivity_Click;
        }
        void Test()
        {
            var z = new AndroidNetworkInfoProvider();
            var y = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (var item in y)
            {
                foreach (var item2 in item.GetAddressBytes())
                {
                    System.Diagnostics.Trace.WriteLine(item2);
                }

            }
            foreach (var item in z.TryGetCurrentNetworkInterfaces())
            {
                //  item.
                S(item.InterfaceAddresses);
            }



        }
        void S(object obj) { }
        SimpleTCP.Reactive.SimpleReactiveTcpClient client = new
         SimpleTCP.Reactive.SimpleReactiveTcpClient();
        //SocketLite.Services.TcpSocketListener();

        static SimpleTCP.Reactive.SimpleReactiveTcpServer server;
        private void MainActivity_Click(object sender, System.EventArgs e)
        {
            server = new SimpleTCP.Reactive.SimpleReactiveTcpServer();

            server.ClientConnected.Subscribe(x =>
            {
                Snackbar.Make(view, x.Client.RemoteEndPoint + " Connected", Snackbar.LengthLong)
              .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
                //Console.WriteLine(x.Client.RemoteEndPoint + " Connected");
            });
            server.Start(2003);

            //var endPoint = new IPEndPoint(IPAddress.Parse("192.168.2.100"), 2003);
            //client.Connect(endPoint.Address.ToString(), endPoint.Port);



        }
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }
        View view;
        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            view = (View)sender;
            Snackbar.Make(view, "tekst", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
