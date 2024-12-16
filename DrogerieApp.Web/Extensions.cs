using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace DrogerieApp.Web
{
    public static class Extensions
    {
        public static ValueTask LoadGoogleMapsApiAsync(this IJSRuntime runtime)
        {
            return runtime.InvokeVoidAsync("CustomGoogleMapsJs.loadMapsApi", new
            {
                apiKey = "AIzaSyALtZyzX1LZyolPytf6rTxHJbFJGcHmPMI"
            });
        }

        public static ValueTask InitGoogleMapsAsync(this IJSRuntime runtime, params object?[]? args)
        {
            return runtime.InvokeVoidAsync("CustomGoogleMapsJs.initMapAsync", args);
        }
    }
}
