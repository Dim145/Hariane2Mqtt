using System.Text.Json.Serialization;

namespace Hariane2Mqtt.Hariane;

public class InfosContrat
{
    [JsonPropertyName("abo_num")] public string AboNum { get; set; }
    [JsonPropertyName("abo_nom")] public string AboNom { get; set; }
    [JsonPropertyName("abo_prn")] public string AboPrn { get; set; }
    [JsonPropertyName("abo_qualite")] public string AboQualite { get; set; }
    [JsonPropertyName("abo_nom2")] public string AboNom2 { get; set; }
    [JsonPropertyName("abo_prn2")] public string AboPrn2 { get; set; }
    [JsonPropertyName("abo_qualite2")] public string AboQualite2 { get; set; }
    [JsonPropertyName("cnc_adr")] public string CncAdr { get; set; }
    [JsonPropertyName("cnc_cmp_adr")] public string CncCmpAdr { get; set; }

    [JsonPropertyName("cnc_cod_post_ville")]
    public string CncCodPostVille { get; set; }

    [JsonPropertyName("banque")] public string Banque { get; set; }
    [JsonPropertyName("guichet")] public string Guichet { get; set; }
    [JsonPropertyName("compte")] public string Compte { get; set; }
    [JsonPropertyName("rib")] public string Rib { get; set; }
    [JsonPropertyName("nom_compte")] public string NomCompte { get; set; }
    [JsonPropertyName("bic")] public string Bic { get; set; }
    [JsonPropertyName("iban")] public string Iban { get; set; }
    [JsonPropertyName("etat_contrat")] public string EtatContrat { get; set; }
    [JsonPropertyName("mode_fact")] public string ModeFact { get; set; }
    [JsonPropertyName("num_cnc")] public string NumCnc { get; set; }
    [JsonPropertyName("num_cnc_full")] public string NumCncFull { get; set; }

    [JsonPropertyName("dte_rdv_autre_contrat")]
    public string DteRdvAutreContrat { get; set; }

    [JsonPropertyName("boo_telereleve")] public float BooTelereleve { get; set; }
    [JsonPropertyName("m2o_num_cpt")] public string M2ONumCpt { get; set; }
    [JsonPropertyName("mode_regle")] public string ModeRegle { get; set; }
    [JsonPropertyName("lib_mode_regle")] public string LibModeRegle { get; set; }
    [JsonPropertyName("nb_pers")] public int NbPers { get; set; }
    [JsonPropertyName("pay_qualite")] public string PayQualite { get; set; }
    [JsonPropertyName("pay_nom")] public string PayNom { get; set; }
    [JsonPropertyName("pay_prn")] public string PayPrn { get; set; }

    [JsonPropertyName("pay_adr_ident_remise")]
    public string PayAdrIdentRemise { get; set; }

    [JsonPropertyName("pay_adr")] public string PayAdr { get; set; }
    [JsonPropertyName("pay_cmp_adr")] public string PayCmpAdr { get; set; }

    [JsonPropertyName("pay_cod_post_ville")]
    public string PayCodPostVille { get; set; }

    [JsonPropertyName("pay_pays")] public string PayPays { get; set; }
    [JsonPropertyName("rum")] public string Rum { get; set; }
}