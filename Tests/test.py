import glob
import os
import subprocess

os.chdir(os.path.dirname(os.path.realpath(__file__)) + "/..")

os.system("dotnet build")
for f in glob.glob("Tests/*.dcx"):
    print(f"Decompiling {f}")
    subprocess.check_output(
        f"dotnet run --no-build --project CLI decompile {f} Tests/.new.js"
    )

    print(f"Recompiling {f}")
    subprocess.check_output(
        f"dotnet run --no-build --project CLI decompile {f} --js-type=mattscript Tests/.matt.js"
    )
    out = subprocess.check_output(
        "dotnet run --no-build --project CLI preview --js-type=mattscript Tests/.new.js"
    )
    out = out.decode()
    open("Tests/.out.js", "w", newline="").write(out)
    print(f"Checking diff {f}")
    subprocess.call("code.cmd --diff --wait Tests/.matt.js Tests/.out.js")

for f in glob.glob("Tests/*.js"):
    os.unlink(f)
