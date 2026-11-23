# Afternoon Walk

Automação UiPath que avisa quando o tempo está bom para o passeio do fim da tarde. Ela consulta a
OpenWeather para Santa Maria (RS), envia um e-mail pelo Gmail e faz um anúncio na Alexa pelo
Voice Monkey. É uma adaptação de um fluxo que antes rodava no n8n.

## Como funciona

A automação roda a cada 10 minutos entre 18:00 e 19:50 e mais uma vez às 20:00 (horário de Brasília).
Em cada execução:

1. **Já avisou hoje?** Se o aviso já foi enviado hoje, termina sem fazer nada. A data do último aviso
   fica em `%LOCALAPPDATA%\AfternoonWalk\last-sent.txt`.
2. **Antes do horário de início?** Antes do valor do asset `StartHour`, termina sem consultar nada.
3. **Tempo atual** (uma única chamada à API gratuita 2.5 *current weather* da OpenWeather): o tempo
   precisa estar aberto (código de condição ≥ 800).
4. **Temperatura:** avisa se a sensação térmica estiver abaixo do asset `Temperature`. Se ainda
   estiver acima, só avisa a partir do horário de `EndHour` (a última verificação do dia).
5. **Aviso:**
   - envia um e-mail em português pela conexão do Gmail no Integration Service;
   - grava a data de hoje no arquivo de estado;
   - anuncia na Alexa pelo Voice Monkey o texto do asset `VoiceMonkeySpeech`. Se o anúncio falhar,
     o e-mail já enviado vale e a falha fica só registrada no log.

> As verificações de previsão para os próximos 15 minutos e das últimas 12 horas do fluxo original
> foram retiradas: elas exigem a assinatura *One Call 3.0* da OpenWeather.

## Estrutura do projeto

| Arquivo | Função |
|---|---|
| `Main.xaml` | Ponto de entrada: chama as verificações, envia o e-mail, grava o estado e chama o anúncio. |
| `CheckWalkConditions.cs` | Workflow em código (C#) com as regras de horário, tempo e temperatura. |
| `AnnounceOnAlexa.cs` | Workflow em código (C#) que chama a API do Voice Monkey. |
| `project.json` | Manifesto do projeto (dependências, pontos de entrada). |

Projeto Windows, expressões em C#, criado no UiPath Studio 26.10.

## Configuração no Orchestrator

Todos os assets ficam na pasta **AfternoonWalk** do Orchestrator.

| Asset | Tipo | Exemplo | Uso |
|---|---|---|---|
| `OpenWeatherMapApiKey` | Credential | chave no campo senha | Chave da API da OpenWeather |
| `VoiceMonkeyApiKey` | Credential | token no campo senha | Token da API do Voice Monkey |
| `VoiceMonkeyDeviceID` | Text | `alexa-sala` | Dispositivo do Voice Monkey |
| `VoiceMonkeySpeech` | Text | `Hora do passeio!` | Texto falado pela Alexa |
| `Recipient` | Text | `voce@exemplo.com` | Destinatário do e-mail |
| `Latitude` | Text | `-29.6842` | Latitude do local |
| `Longitude` | Text | `-53.8069` | Longitude do local |
| `StartHour` | Text | `18:00` | Início das verificações |
| `EndHour` | Text | `20:00` | Última verificação do dia |
| `Temperature` | Integer | `26` | Limite da sensação térmica em °C |

Números aceitam ponto ou vírgula; horários aceitam `18:00` ou só `18`.

### Argumentos do `Main.xaml`

| Argumento | Padrão | Descrição |
|---|---|---|
| `in_RecipientAsset` | `Recipient` | Nome do asset com o destinatário do e-mail |
| `in_AssetFolder` | `AfternoonWalk` | Pasta dos assets (vazio = pasta do job) |
| `in_TimeZoneId` | `America/Sao_Paulo` | Fuso usado para horários e para a data |
| `in_OpenWeatherCredentialAsset` | `OpenWeatherMapApiKey` | Nome do asset da chave da OpenWeather |
| `in_VoiceMonkeyCredentialAsset` | `VoiceMonkeyApiKey` | Nome do asset do token do Voice Monkey |
| `in_VoiceMonkeyDeviceAsset` | `VoiceMonkeyDeviceID` | Nome do asset do dispositivo |
| `in_VoiceMonkeySpeechAsset` | `VoiceMonkeySpeech` | Nome do asset do texto falado |

### Conexão do Gmail

O e-mail usa uma conexão do Gmail no **Integration Service**. Compartilhe essa conexão com a pasta
AfternoonWalk; sem isso, jobs não assistidos não conseguem enviar o e-mail. Se criar uma conexão
nova, atualize o `ConnectionId` da atividade *Send Email* no `Main.xaml`.

### Agendamento

O processo "Afternoon Walk" tem dois gatilhos de tempo, no fuso *E. South America Standard Time*
(Brasília), executando em robô não assistido:

| Gatilho | Cron |
|---|---|
| A cada 10 minutos | `0 0/10 18-19 ? * *` |
| Verificação final | `0 0 20 ? * *` |

Para rodar no próprio computador:

1. Crie um *machine template* com 1 slot não assistido e atribua à pasta AfternoonWalk.
2. Em *Manage Access*, habilite o robô não assistido do seu usuário com as credenciais do Windows.
3. No UiPath Assistant, conecte em *Preferences → Orchestrator Settings* usando a *Machine Key*.

O computador precisa estar ligado entre 18:00 e 20:00.

## Desenvolvimento

Com a [UiPath CLI](https://docs.uipath.com/coding-agents/standalone/latest/user-guide/install-and-set-up):

```powershell
uip rpa validate --file-path Main.xaml --project-dir .
```

```powershell
uip rpa build --project-dir .
```

```powershell
uip rpa run --file-path Main.xaml --project-dir . --skip-build
```

Para executar antes de 18:00, mude temporariamente o asset `StartHour`. Depois de um aviso enviado,
apague `%LOCALAPPDATA%\AfternoonWalk\last-sent.txt` para poder testar de novo no mesmo dia.
