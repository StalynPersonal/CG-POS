// Publicidad del segundo monitor.
//
// Un navegador solo reproduce un video sin que nadie lo toque si va sin sonido, y no basta con el atributo: hay que
// ponérselo a la propiedad y pedir la reproducción, porque el elemento lo crea Blazor después de cargar la página.
window.cgPantallaCliente = {
    // Devuelve si el video quedó reproduciéndose. Si el navegador lo rechaza (pestaña en segundo plano, política del
    // equipo), la pantalla pasa a la pieza siguiente: nunca se queda trabada en el primer cuadro.
    reproducir: async function (video) {
        if (!video) {
            return false;
        }

        video.muted = true;
        video.defaultMuted = true;

        try {
            await video.play();
            return true;
        } catch {
            return false;
        }
    },
};
