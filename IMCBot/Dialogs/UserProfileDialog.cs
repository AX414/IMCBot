// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;

namespace Microsoft.BotBuilderSamples
{
    public class UserProfileDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<UserProfile> _userProfileAccessor;

        public UserProfileDialog(UserState userState)
            : base(nameof(UserProfileDialog))
        {
            _userProfileAccessor = userState.CreateProperty<UserProfile>("UserProfile");

            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                EtapaPerguntaNomeAsync,
                EtapaPerguntaSexoAsync,
                EtapaPerguntaAlturaAsync,
                EtapaPerguntaPesoAsync,
                EtapaIMCAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>), AlturaValidatorAsync));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>), PesoValidatorAsync));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

         // Pede o nome
        private static async Task<DialogTurnResult> EtapaPerguntaNomeAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { 
                Prompt = MessageFactory.Text("Por favor, insira seu nome.") 
            }, cancellationToken);
        }

        
        private static async Task<DialogTurnResult> EtapaPerguntaSexoAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["nome"] = (string)stepContext.Result;

            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Obrigado, {stepContext.Result}."), cancellationToken);

            return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Selecione seu sexo."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Masculino", "Feminino"}),
                }, cancellationToken);
        }


        // Usuário confirma o nome e pede a altura
        private async Task<DialogTurnResult>EtapaPerguntaAlturaAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["sexo"] = ((FoundChoice)stepContext.Result).Value;

            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text("Insira sua Altura."),
                RetryPrompt = MessageFactory.Text("A altura deve ser maior que 0."),
            };

            return await stepContext.PromptAsync(nameof(NumberPrompt<int>), promptOptions, cancellationToken);
        }
        
        // Usuário confirma a altura e pede o peso
        private async Task<DialogTurnResult> EtapaPerguntaPesoAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["altura"] = (int)stepContext.Result;

            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text("Informe seu peso."),
                RetryPrompt = MessageFactory.Text("O peso deve ser maior que 0."),
            };

            return await stepContext.PromptAsync(nameof(NumberPrompt<int>),promptOptions, cancellationToken);
        }
        
        // Faz o Cálculo do IMC
        private async Task<DialogTurnResult> EtapaIMCAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            stepContext.Values["peso"] = (int)stepContext.Result;

            // Instancia o userProfile
            var userProfile = await _userProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

            userProfile.Nome = (string)stepContext.Values["nome"];
            userProfile.Sexo = (string)stepContext.Values["sexo"];
            userProfile.Altura = (int)stepContext.Values["altura"];
            userProfile.Peso = (int)stepContext.Values["peso"];

            // Mensagem
            var msg = $"+=====DADOS SALVOS=====+\r\nNome: {userProfile.Nome}\r\nSexo: {userProfile.Sexo}\r\nAltura: {userProfile.Altura} cm\r\nPeso: {userProfile.Peso} Kg";

            // Link de referência: https://www.sallet.com.br/o-que-e-peso-ideal-e-como-calcula-lo/#:~:text=O%20c%C3%A1lculo%20%C3%A9%20bastante%20simples,%C3%B7%20Altura%20(m)%C2%B2.
            double IMC = userProfile.Peso/(userProfile.Altura * userProfile.Altura);
            
            msg += $"\r\nIMC: {IMC}";

            /*
            Menor que 18,5 = abaixo do peso.
            Entre 18,5 e 24,9 = peso normal.
            Entre 25 e 29,9 = sobrepeso.
            Entre 30 e 34,99 = obesidade grau I.
            Entre 35 e 39,99 = obesidade grau II (severa).
            Acima de 40 = obesidade grau III (mórbida).
            */
         
            var resposta = $"Sem resposta";
            if(IMC <= 18.5)
            {
                resposta = $"Abaixo do peso";
            }else if(IMC >=25 && IMC <= 29.9)
            {
                resposta = $"Peso normal";
            }else if(IMC >=30 && IMC <= 34.99){
                resposta = $"Obesidade Grau I";
            }else if(IMC >=35 && IMC <= 39.99)
            {
                resposta = $"Obesidade Grau II";
            }else if(IMC >= 40)
            {
                resposta = $"Obesidade Grau III";
            }

            msg += $"\r\n\r\nResultado: {resposta}";
         
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
           
         return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }


        //===========VALIDATORS======================

        // Validação da altura
        private static Task<bool> AlturaValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            // This condition is our validation rule. You can also change the value at this point.
            return Task.FromResult(promptContext.Recognized.Succeeded && promptContext.Recognized.Value > 0);
        }

        // Validação do peso
        private static Task<bool> PesoValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            // This condition is our validation rule. You can also change the value at this point.
            return Task.FromResult(promptContext.Recognized.Succeeded && promptContext.Recognized.Value > 0);
        }

    }
}
