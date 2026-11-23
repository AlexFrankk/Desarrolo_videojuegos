@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set "LOG=.git\audio_merge.log"
echo ==== %date% %time% ==== > "%LOG%"

tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
if not errorlevel 1 (
  echo Unity esta abierto. Cierra Unity ^(guarda primero^) y vuelve a ejecutar este archivo.
  echo [abortado] Unity abierto>> "%LOG%"
  pause
  exit /b 1
)

echo === 1/5 Guardando cambios locales en un respaldo (stash) ===
git status --porcelain >> "%LOG%" 2>&1
git status --porcelain | findstr /R /V "traer_audio.bat" | findstr /R "." >nul
if not errorlevel 1 (
  git stash push -u -m "respaldo antes de traer audio" -- . ":(exclude)traer_audio.bat" >> "%LOG%" 2>&1
  echo [stash] cambios locales guardados en "respaldo antes de traer audio">> "%LOG%"
)

echo === 2/5 Descargando main y la rama alice del repositorio de Hector ===
git fetch origin >> "%LOG%" 2>&1
if errorlevel 1 goto fallo

echo === 3/5 Actualizando main ===
git checkout main >> "%LOG%" 2>&1
if errorlevel 1 goto fallo
git merge --ff-only origin/main >> "%LOG%" 2>&1
if errorlevel 1 goto fallo

echo === 4/5 Uniendo el audio de alice ===
git merge --no-ff origin/alice -m "Merge rama alice: audio de ambiente por nivel y sonido de ataque del mago" >> "%LOG%" 2>&1
if not errorlevel 1 goto unido
git checkout --ours -- Assets/MageHero/Resources.meta >> "%LOG%" 2>&1
git add Assets/MageHero/Resources.meta >> "%LOG%" 2>&1
git diff --name-only --diff-filter=U > ".git\conflictos.txt"
for %%A in (".git\conflictos.txt") do if %%~zA GTR 0 goto conflictos
git commit --no-edit >> "%LOG%" 2>&1
if errorlevel 1 goto fallo

:unido
echo [merge] OK>> "%LOG%"
echo === 5/5 Actualizando tu rama Alex con todo ===
git checkout Alex >> "%LOG%" 2>&1
git merge --ff-only main >> "%LOG%" 2>&1
if errorlevel 1 git merge --no-edit main >> "%LOG%" 2>&1
git log --oneline -6 >> "%LOG%" 2>&1
git status -sb >> "%LOG%" 2>&1
dir /b "Assets\MageHero\Resources\Audio" >> "%LOG%" 2>&1
echo.
echo Listo: tienes main + el audio de Alice en tu rama Alex. Ya puedes abrir Unity.
goto fin

:conflictos
echo [conflictos] quedan archivos en conflicto:>> "%LOG%"
type ".git\conflictos.txt" >> "%LOG%"
echo.
echo Hay conflictos que no pude resolver solo. No toques nada y avisa a Claude.
goto fin

:fallo
echo [fallo] codigo %errorlevel%>> "%LOG%"
git status >> "%LOG%" 2>&1
echo.
echo Algo fallo. No toques nada y avisa a Claude.

:fin
echo Detalle en %LOG%
pause
(goto) 2>nul & del "%~f0"
