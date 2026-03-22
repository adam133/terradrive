using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VectorRoad.Hud;

namespace VectorRoad.Tests.PlayMode
{
    /// <summary>
    /// Play-mode tests for <see cref="CoordinateEntryHud"/> dialog toggle logic.
    /// </summary>
    public class CoordinateEntryHudPlayModeTests
    {
        private GameObject _go;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null)
            {
                Object.Destroy(_go);
                yield return null;
            }
        }

        // ── Reflection helpers ─────────────────────────────────────────────────

        private static bool IsVisible(CoordinateEntryHud hud) =>
            (bool)typeof(CoordinateEntryHud)
                .GetField("_isVisible", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(hud);

        // ── Show / Hide / Toggle API ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator Show_SetsVisible()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Show();

            Assert.That(IsVisible(hud), Is.True, "Show() should make the dialog visible.");
        }

        [UnityTest]
        public IEnumerator Hide_ClearsVisible()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Show();
            hud.Hide();

            Assert.That(IsVisible(hud), Is.False, "Hide() should close the dialog.");
        }

        [UnityTest]
        public IEnumerator Toggle_OpensDialogWhenClosed()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Toggle();

            Assert.That(IsVisible(hud), Is.True, "Toggle() on a closed dialog should open it.");
        }

        [UnityTest]
        public IEnumerator Toggle_ClosesDialogWhenOpen()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Show();
            hud.Toggle();

            Assert.That(IsVisible(hud), Is.False, "Toggle() on an open dialog should close it.");
        }

        [UnityTest]
        public IEnumerator Toggle_DoesNotCloseWhileLoading()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Show();
            typeof(CoordinateEntryHud)
                .GetField("_isLoading", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(hud, true);

            hud.Toggle();

            Assert.That(IsVisible(hud), Is.True,
                "Toggle() should not close the dialog while a download is in progress.");
        }

        [UnityTest]
        public IEnumerator ShowTwice_RemainsVisible()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Show();
            hud.Show();

            Assert.That(IsVisible(hud), Is.True, "Calling Show() twice should leave dialog visible.");
        }

        [UnityTest]
        public IEnumerator Hide_WhenAlreadyHidden_RemainsHidden()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Hide();

            Assert.That(IsVisible(hud), Is.False, "Hide() when already hidden should keep dialog hidden.");
        }

        [UnityTest]
        public IEnumerator Toggle_MultipleRounds_AlternatesVisibility()
        {
            _go = new GameObject("HUD");
            var hud = _go.AddComponent<CoordinateEntryHud>();
            yield return null;

            hud.Toggle(); Assert.That(IsVisible(hud), Is.True,  "1st toggle: open");
            hud.Toggle(); Assert.That(IsVisible(hud), Is.False, "2nd toggle: closed");
            hud.Toggle(); Assert.That(IsVisible(hud), Is.True,  "3rd toggle: open");
        }
    }
}
