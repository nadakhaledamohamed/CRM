// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// site.js

function toggleTheme() {
    const link = document.getElementById('theme-stylesheet');
    const currentTheme = link.getAttribute('href');
    const newTheme = currentTheme.includes('dark') ? '/css/light.css' : '/css/dark.css';
    link.setAttribute('href', newTheme);
    localStorage.setItem('theme', newTheme);
}

// Run on page load to restore the theme
document.addEventListener('DOMContentLoaded', () => {
    const savedTheme = localStorage.getItem('theme') || '/css/dark.css';
    const link = document.getElementById('theme-stylesheet');
    if (link) {
        link.setAttribute('href', savedTheme);
    }
});
