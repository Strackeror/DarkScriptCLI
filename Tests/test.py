import glob
import os
import subprocess
import sys

os.chdir(os.path.dirname(os.path.realpath(__file__)) + "/..")

jstype="mattscript"

newFile = "Tests/new.js"
checkFile = "Tests/check.js"
outFile = "Tests/out.js"

os.system("dotnet build")

files = sys.argv[1:] or glob.glob("Tests/*.dcx")

for f in files:
    try:
        print(f"Decompiling {f}")
        subprocess.check_output(
            f"dotnet run --no-build --project CLI decompile {f} {newFile}"
        )

        print(f"Recompiling {f}")
        subprocess.check_output(
            f"dotnet run --no-build --project CLI decompile {f} --js-type={jstype} {checkFile}"
        )
        out = subprocess.check_output(
            f"dotnet run --no-build --project CLI preview --js-type={jstype} {newFile}"
        )
        out = out.decode()
        open(outFile, "w", newline="").write(out)
        print(f"Checking diff {f}")
        subprocess.call(f"code.cmd --new-window --diff --wait {checkFile} {outFile}")
    except subprocess.CalledProcessError as e:
        print(e)
        print(e.output.decode())
