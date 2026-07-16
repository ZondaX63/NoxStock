$(function () {
    var $themeMode = $('#ThemeMode');
    var savedTheme = localStorage.getItem('noxstock.theme');
    if (!savedTheme) {
        savedTheme = localStorage.getItem('darkMode') === 'true' ? 'dark' : 'light';
    }

    $themeMode.val(savedTheme);
    $themeMode.on('change', function () {
        var isDark = this.value === 'dark';
        localStorage.setItem('noxstock.theme', this.value);
        localStorage.setItem('darkMode', isDark ? 'true' : 'false');
        document.documentElement.classList.toggle('dark', isDark);
        if (window.Alpine) {
            window.Alpine.$data(document.body).darkMode = isDark;
        }
    });
});
