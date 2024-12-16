(function () {
    const importDynamicLibrary = (g => {
        var h, a, k,
            p = "The Google Maps JavaScript API",
            c = "google",
            l = "importLibrary",
            q = "__ib__",
            m = document,
            b = window;

        b = b[c] || (b[c] = {});
        var d = b.maps || (b.maps = {}),
            r = new Set,
            e = new URLSearchParams,
            u = () => h || (h = new Promise(async (f, n) => {
                a = m.createElement("script");
                e.set("libraries", [...r] + "");
                for (k in g) e.set(k.replace(/[A-Z]/g, t => "_" + t[0].toLowerCase()), g[k]);
                e.set("callback", c + ".maps." + q);
                a.src = `https://maps.${c}apis.com/maps/api/js?` + e;
                d[q] = f;
                a.onerror = () => h = n(Error(p + " could not load."));
                a.nonce = m.querySelector("script[nonce]")?.nonce || "";
                m.head.append(a);
            }));

        d[l] ? console.warn(p + " only loads once. Ignoring:", g) : d[l] = (f, ...n) => r.add(f) && u().then(() => d[l](f, ...n));
    });

    window.CustomGoogleMapsJs = {
        importDynamicLibrary: importDynamicLibrary,

        loadMapsApi: function ({ apiKey, version = "quarterly", libraries = "places,marker", language = "de", region = "CH" }) {
            this.importDynamicLibrary({
                key: apiKey,
                v: version,
                libraries,
                language,
                region
            });
        },

        map: null,

        initMapAsync: async function ({
            elementId = "map",
            mapId = "4504f8b37309c3d0",
            center = { lat: 47.4185, lng: 9.353 },
            zoom = 10,
            controls = {}
        } = {}) {
            const { Map } = await google.maps.importLibrary("maps");
            this.map = new Map(document.getElementById(elementId), {
                center,
                zoom,
                mapId,
                ...controls
            });
        },

        searchAsync: async function ({
            location = { lat: 47.4185, lng: 9.353 },
            radius = 5000,
            includedPrimaryTypes = ["drugstore"]
        } = {}) {
            const { Place, SearchNearbyRankPreference } = await google.maps.importLibrary("places");
            const { AdvancedMarkerElement } = await google.maps.importLibrary("marker");
            const request = {
                fields: ["displayName", "location", "businessStatus", "photos", "adrFormatAddress"],
                locationRestriction: {
                    center: location,
                    radius: radius
                },
                includedPrimaryTypes: includedPrimaryTypes
            };
            const { places } = await Place.searchNearby(request)
            const infoWindow = new google.maps.InfoWindow({ name: "" });
            if (places.length) {
                const { LatLngBounds } = await google.maps.importLibrary("core");
                const bounds = new LatLngBounds();

                // Loop through and get all the results.
                places.forEach((place) => {
                    const marker = new AdvancedMarkerElement({
                        map: this.map,
                        position: place.location,
                        title: place.displayName,
                        gmpClickable: true
                    });
                    marker.addListener("click", ({ domEvent, latLng }) => {
                        infoWindow.close();
                        infoWindow.setContent(marker.title);
                        infoWindow.open(marker.map, marker);
                    });
                    bounds.extend(place.location);
                });
                this.map.fitBounds(bounds);
                const simpleResults = places.map(r => ({
                    Name: r.displayName,
                    Location: r.location,
                    Status: r.businessStatus,
                    Photo: r.photos[0],
                    Address: r.adrFormatAddress
                }));
                return simpleResults;
            }
        }
    };
})();