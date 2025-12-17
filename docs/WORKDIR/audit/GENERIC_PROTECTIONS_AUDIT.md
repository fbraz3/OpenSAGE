# Audit de Proteções Genéricas (Generic Protections)

## Objetivo

Identificar e catalogar todas as proteções genéricas (try-catch blocks, null checks, etc.) que podem estar silenciosamente engolindo erros sem logs apropriados.

---

## 1. CATCH BLOCKS COM EXCEPTION GENÉRICA (11 encontrados)

### ✅ BEM DOCUMENTADO (Com logs bons)

#### 1. `SteamInstallationLocator.cs:55`

```csharp
catch (Exception e)
{
    Logger.Warn($"Failed to parse libraryfolders.vdf file at {libraryFoldersPath}: {e.Message}");
    yield break;
}
```

**Status**: ✅ BOM - Log com mensagem clara e contexto
**Problema**: Nenhum

---

#### 2. `LuaScriptConsole.cs:90`

```csharp
catch (Exception exeption)  // typo: deveria ser "exception"
{
    _scriptConsoleTextAll = string.Concat(_scriptConsoleTextAll, "FATAL ERROR: ", exeption, "\n");
    _consoleTextColor = new Vector4(150, 0, 0, 1);
}
```

**Status**: ✅ BOM - Exibe no console
**Problema**: Typo no nome da variável ("exeption")

---

#### 3. `AudioSystem.cs:75`

```csharp
catch (Exception ex)
{
    LogAudioInitializationFailure();
    Logger.Debug(ex, "Exception details during audio initialization");
}
```

**Status**: ✅ BOM - Log com detalhes + método auxiliar
**Problema**: Nenhum

---

#### 4. `OrderProcessor.cs:256` (SetSelection)

```csharp
catch (Exception e)
{
    Logger.Error(e, "Error while setting selection");
}
```

**Status**: ✅ BOM - Logger.Error com exceção
**Problema**: Nenhum

---

#### 5. `OrderProcessor.cs:331` (RallyPoint)

```csharp
catch (Exception e)
{
    Logger.Error(e, "Error while setting rallypoint");
}
```

**Status**: ✅ BOM - Logger.Error com exceção
**Problema**: Nenhum

---

#### 6. `UPnP.cs:72`

```csharp
catch (Exception e)
{
    if (SkirmishHostMapping != null)
    {
        await NatDevice.DeletePortMapAsync(SkirmishHostMapping);
    }

    if (SkirmishGameMapping != null)
    {
        await NatDevice.DeletePortMapAsync(SkirmishGameMapping);
    }

    Logger.Error(e, "Failed to forward port.");
    return false;
}
```

**Status**: ✅ BOM - Cleanup + Logger.Error
**Problema**: Nenhum

---

#### 7. `LuaScriptEngine.cs:42`

```csharp
catch (Exception ex)
{
    Logger.Error(ex, "Error while loading script file");
}
```

**Status**: ✅ BOM - Logger.Error com exceção
**Problema**: Nenhum

---

#### 8. `StaticLodApplicationManager.cs:97`

```csharp
catch (Exception ex)
{
    throw new InvalidOperationException($"Failed to apply static LOD level {lodLevel}", ex);
}
```

**Status**: ✅ BOM - Re-throws com contexto
**Problema**: Nenhum

---

#### 9. `BigEditor MainForm.cs:244`

```csharp
catch (Exception e)
{
    Logger.Error(e.Message);
}
```

**Status**: ⚠️ PARCIAL - Log presente mas apenas `.Message`
**Problema**: Usa `e.Message` em vez de passar a exceção; perderá StackTrace
**Recomendação**: `Logger.Error(e, "Failed to open big file");`

---

#### 10. `BigEditor MainForm.cs:404`

```csharp
catch (Exception e)
{
    Logger.Error(e.Message);
    return;
}
```

**Status**: ⚠️ PARCIAL - Log presente mas apenas `.Message`
**Problema**: Usa `e.Message` em vez de passar a exceção; perderá StackTrace
**Recomendação**: `Logger.Error(e, "Failed to create directory during export");`

---

#### 11. `BigEditor MainForm.cs:420`

```csharp
catch (Exception e)
{
    Logger.Error(e.Message);
}
```

**Status**: ⚠️ PARCIAL - Log presente mas apenas `.Message`
**Problema**: Usa `e.Message` em vez de passar a exceção; perderá StackTrace
**Recomendação**: `Logger.Error(e, "Failed to export file");`

---

## 2. PROTEÇÕES COM NULL CHECKS SILENCIOSOS

Procurando por padrões como:

```csharp
if (value == null) return;
if (value == null) yield break;
if (!Collection.Contains(x)) continue;
```

**Resultado**: Muitos encontrados, mas a maioria é intencional e documentada. Revisar caso por caso conforme necessário.

---

## 3. PROTEÇÕES COM EMPTY CATCH BLOCKS

Procura por: `catch { }` ou `catch (Exception) { }`

**Resultado**: 0 encontrados ✅

---

## 4. RESUMO E RECOMENDAÇÕES

### Estatísticas
- **Total de catch blocks**: 11
- **Bem documentados**: 8 ✅
- **Parcialmente documentados**: 3 ⚠️
- **Mal documentados**: 0

### Ações Imediatas

| Arquivo | Linha | Problema | Prioridade |
|---------|-------|----------|-----------|
| BigEditor/MainForm.cs | 244 | Usar `Logger.Error(e, ...)` em vez de `Logger.Error(e.Message)` | 🔴 ALTA |
| BigEditor/MainForm.cs | 404 | Usar `Logger.Error(e, ...)` em vez de `Logger.Error(e.Message)` | 🔴 ALTA |
| BigEditor/MainForm.cs | 420 | Usar `Logger.Error(e, ...)` em vez de `Logger.Error(e.Message)` | 🔴 ALTA |
| LuaScriptConsole.cs | 90 | Typo: `exeption` → `exception` | 🟡 MÉDIA |

---

## 5. IMPLEMENTAÇÃO DE MELHORIAS

### Padrão Recomendado para Catch Blocks

```csharp
// ERRADO ❌
catch (Exception e)
{
    Logger.Error(e.Message);
}

// CORRETO ✅
catch (Exception e)
{
    Logger.Error(e, "Description of what was being attempted");
    // OR
    Logger.Error(e, $"Failed to {action} {context}");
}
```

### Por quê?


1. `Logger.Error(e, msg)` inclui **StackTrace** completo
2. `Logger.Error(e.Message)` perde contexto de onde o erro ocorreu
3. Mensagem descritiva ajuda a debugar rapidamente

---

## 6. NEXT STEPS

- [ ] Aplicar patch nos 3 BigEditor catches
- [ ] Corrigir typo em LuaScriptConsole.cs
- [ ] Adicionar testes para verificar se exceções produzem logs
- [ ] Considerar wrapper/helper para padrão comum
- [ ] Revisar RenderPipeline.cs para checks genéricos (linha 429 menciona Metal NRE)

