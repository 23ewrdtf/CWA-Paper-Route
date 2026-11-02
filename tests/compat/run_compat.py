"""CWA compatibility test: real Paper Route SMTP host + CWA's exact sending code.

Usage: python tests/compat/run_compat.py <command that starts PaperRoute.CompatHost>
The host prints "READY <port> <password> <outdir>" when listening.
"""

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SENDER = os.path.join(HERE, "cwa_send.py")


def run_sender(port: int, *args: str) -> tuple[int, str]:
    result = subprocess.run(
        [sys.executable, SENDER, "--port", str(port), *args],
        capture_output=True, text=True, timeout=120, check=False,
    )
    return result.returncode, (result.stdout + result.stderr).strip()


def main() -> int:
    host = subprocess.Popen(sys.argv[1:], stdout=subprocess.PIPE, stdin=subprocess.PIPE, text=True)
    try:
        ready = host.stdout.readline().split()
        if len(ready) != 4 or ready[0] != "READY":
            print(f"Host did not start: {ready}")
            return 1
        port, password, outdir = int(ready[1]), ready[2], ready[3]

        cases = [
            ("book is delivered", ["--password", password, "--attachment", "Zagubiona przyszłość.azw3"], 0, "E-mail sent"),
            ("test e-mail is accepted", ["--password", password], 0, "E-mail sent"),
            ("CWA test e-mail to the admin's own address", ["--password", password, "--to", "admin@example.org"], 0, "E-mail sent"),
            ("wrong password", ["--password", "wrong"], 2, "Wrong login or password - copy them again from Paper Route's Settings window"),
            ("no password set in CWA", ["--attachment", "a.azw3"], 2, "Authentication required - enter Paper Route's login and password in CWA's e-mail settings"),
            ("EPUB without Calibre", ["--password", password, "--attachment", "a.epub"], 2, "Install Calibre (calibre-ebook.com) so Paper Route can convert EPUB books"),
            ("other recipient", ["--password", password, "--to", "someone@example.com", "--attachment", "a.azw3"], 2, "Unknown recipient - Paper Route only accepts mail for kindle@paperroute.local"),
        ]
        failures = 0
        for name, args, want_code, want_text in cases:
            code, output = run_sender(port, *args)
            ok = code == want_code and want_text in output
            failures += not ok
            print(f"{'PASS' if ok else 'FAIL'}  {name}: exit {code}, {output}")

        delivered = os.path.join(outdir, "Zagubiona przyszłość.azw3")
        if os.path.isfile(delivered) and os.path.getsize(delivered) == 2048:
            print("PASS  delivered file has the right name and size")
        else:
            print(f"FAIL  delivered file missing or wrong size: {os.listdir(outdir)}")
            failures += 1
        others = [f for f in os.listdir(outdir) if f != "Zagubiona przyszłość.azw3"]
        if others:
            print(f"FAIL  refused messages wrote files: {others}")
            failures += 1
        return 1 if failures else 0
    finally:
        host.stdin.close()
        host.wait(timeout=30)


if __name__ == "__main__":
    sys.exit(main())
