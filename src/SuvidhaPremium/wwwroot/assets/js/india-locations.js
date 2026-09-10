(function(w,d){
'use strict';
/* Offline India geography helper. All 28 states + 8 union territories are present.
   City is deliberately searchable/free-entry: the bundled list contains major cities
   and district headquarters, while a retailer can type any Indian locality/city that
   is not in the suggestion list. This keeps Outlet Master usable offline. */
const DATA={
"Andaman and Nicobar Islands":["Port Blair","Diglipur","Mayabunder","Rangat","Car Nicobar","Campbell Bay"],
"Andhra Pradesh":["Amaravati","Visakhapatnam","Vijayawada","Guntur","Tirupati","Nellore","Kurnool","Kakinada","Rajamahendravaram","Kadapa","Anantapur","Eluru","Ongole","Srikakulam","Vizianagaram","Machilipatnam","Nandyal","Chittoor","Bhimavaram","Tenali","Hindupur","Proddatur","Adoni"],
"Arunachal Pradesh":["Itanagar","Naharlagun","Tawang","Bomdila","Ziro","Pasighat","Aalo","Tezu","Roing","Namsai","Changlang","Khonsa","Yingkiong","Seppa"],
"Assam":["Dispur","Guwahati","Silchar","Dibrugarh","Jorhat","Nagaon","Tinsukia","Tezpur","Bongaigaon","Dhubri","Sivasagar","North Lakhimpur","Goalpara","Barpeta","Diphu","Haflong","Mangaldoi","Nalbari","Kokrajhar","Hailakandi","Karimganj"],
"Bihar":["Patna","Gaya","Bhagalpur","Muzaffarpur","Darbhanga","Purnia","Bihar Sharif","Arrah","Begusarai","Katihar","Munger","Chhapra","Hajipur","Sasaram","Samastipur","Motihari","Bettiah","Sitamarhi","Madhubani","Saharsa","Siwan","Buxar","Kishanganj","Jamui","Lakhisarai","Jehanabad","Aurangabad","Nawada","Sheikhpura","Araria","Madhepura","Supaul"],
"Chandigarh":["Chandigarh"],
"Chhattisgarh":["Raipur","Bhilai","Durg","Bilaspur","Korba","Rajnandgaon","Raigarh","Jagdalpur","Ambikapur","Dhamtari","Mahasamund","Kanker","Kawardha","Janjgir","Balod","Bemetara","Mungeli","Surajpur","Balrampur","Kondagaon","Dantewada"],
"Dadra and Nagar Haveli and Daman and Diu":["Daman","Diu","Silvassa"],
"Delhi":["New Delhi","Central Delhi","East Delhi","New Delhi District","North Delhi","North East Delhi","North West Delhi","Shahdara","South Delhi","South East Delhi","South West Delhi","West Delhi","Dwarka","Rohini","Karol Bagh","Saket","Janakpuri","Narela"],
"Goa":["Panaji","Margao","Vasco da Gama","Mapusa","Ponda","Bicholim","Curchorem","Valpoi","Pernem","Canacona"],
"Gujarat":["Ahmedabad","Surat","Vadodara","Rajkot","Gandhinagar","Bhavnagar","Jamnagar","Junagadh","Gandhidham","Anand","Navsari","Morbi","Nadiad","Bharuch","Mehsana","Bhuj","Porbandar","Palanpur","Valsad","Vapi","Godhra","Dahod","Botad","Amreli","Patan","Himatnagar","Surendranagar","Veraval","Dwarka"],
"Haryana":["Chandigarh","Faridabad","Gurugram","Panipat","Ambala","Yamunanagar","Rohtak","Hisar","Karnal","Sonipat","Panchkula","Bhiwani","Sirsa","Bahadurgarh","Jind","Kaithal","Rewari","Palwal","Narnaul","Fatehabad","Jhajjar","Kurukshetra","Charkhi Dadri","Nuh"],
"Himachal Pradesh":["Shimla","Dharamshala","Mandi","Solan","Kullu","Hamirpur","Bilaspur","Una","Chamba","Nahan","Kangra","Keylong","Reckong Peo","Palampur","Baddi"],
"Jammu and Kashmir":["Srinagar","Jammu","Anantnag","Baramulla","Sopore","Kathua","Udhampur","Pulwama","Kupwara","Budgam","Rajouri","Poonch","Kulgam","Shopian","Bandipora","Ganderbal","Samba","Reasi","Kishtwar","Doda","Ramban"],
"Jharkhand":["Ranchi","Jamshedpur","Dhanbad","Bokaro Steel City","Deoghar","Hazaribagh","Giridih","Ramgarh","Medininagar","Dumka","Chaibasa","Sahibganj","Godda","Pakur","Gumla","Lohardaga","Simdega","Latehar","Chatra","Koderma","Jamtara","Khunti","Garhwa","Saraikela"],
"Karnataka":["Bengaluru","Mysuru","Mangaluru","Hubballi","Dharwad","Belagavi","Kalaburagi","Davangere","Ballari","Vijayapura","Shivamogga","Tumakuru","Raichur","Bidar","Hassan","Udupi","Chitradurga","Kolar","Mandya","Chikkamagaluru","Bagalkot","Gadag","Haveri","Karwar","Koppal","Chamarajanagar","Madikeri","Chikkaballapur","Yadgir","Ramanagara"],
"Kerala":["Thiruvananthapuram","Kochi","Kozhikode","Kollam","Thrissur","Kannur","Alappuzha","Kottayam","Palakkad","Malappuram","Manjeri","Kasaragod","Pathanamthitta","Idukki","Kattappana","Kalpetta","Muvattupuzha","Thalassery","Ponnani","Tirur"],
"Ladakh":["Leh","Kargil","Diskit","Padum"],
"Lakshadweep":["Kavaratti","Agatti","Amini","Andrott","Kalpeni","Minicoy"],
"Madhya Pradesh":["Bhopal","Indore","Jabalpur","Gwalior","Ujjain","Sagar","Dewas","Satna","Ratlam","Rewa","Katni","Singrauli","Burhanpur","Khandwa","Morena","Bhind","Chhindwara","Guna","Shivpuri","Vidisha","Chhatarpur","Damoh","Mandsaur","Neemuch","Hoshangabad","Narmadapuram","Betul","Sehore","Raisen","Rajgarh","Shajapur","Agar Malwa","Dhar","Jhabua","Alirajpur","Barwani","Khargone","Mandla","Dindori","Balaghat","Seoni","Narsinghpur","Umaria","Shahdol","Anuppur","Sidhi","Tikamgarh","Panna"],
"Maharashtra":["Mumbai","Pune","Nagpur","Nashik","Thane","Navi Mumbai","Aurangabad","Chhatrapati Sambhajinagar","Solapur","Kolhapur","Amravati","Nanded","Sangli","Jalgaon","Akola","Latur","Dhule","Ahmednagar","Ahilyanagar","Chandrapur","Parbhani","Jalna","Bhusawal","Satara","Beed","Yavatmal","Wardha","Gondia","Bhandara","Washim","Hingoli","Osmanabad","Dharashiv","Ratnagiri","Sindhudurg","Palghar","Raigad","Panvel"],
"Manipur":["Imphal","Thoubal","Kakching","Ukhrul","Churachandpur","Senapati","Tamenglong","Jiribam","Bishnupur","Chandel","Kangpokpi"],
"Meghalaya":["Shillong","Tura","Jowai","Nongstoin","Williamnagar","Baghmara","Nongpoh","Mawkyrwat","Khliehriat","Resubelpara","Ampati"],
"Mizoram":["Aizawl","Lunglei","Champhai","Kolasib","Serchhip","Saiha","Lawngtlai","Mamit","Khawzawl","Hnahthial","Saitual"],
"Nagaland":["Kohima","Dimapur","Mokokchung","Tuensang","Wokha","Zunheboto","Mon","Phek","Kiphire","Longleng","Peren","Noklak"],
"Odisha":["Bhubaneswar","Cuttack","Rourkela","Berhampur","Sambalpur","Puri","Balasore","Bhadrak","Baripada","Jharsuguda","Jeypore","Angul","Dhenkanal","Keonjhar","Koraput","Rayagada","Balangir","Bargarh","Nabarangpur","Nuapada","Kalahandi","Phulbani","Boudh","Nayagarh","Kendrapara","Jagatsinghpur","Jajpur","Malkangiri","Sonepur","Paralakhemundi"],
"Puducherry":["Puducherry","Karaikal","Mahe","Yanam"],
"Punjab":["Chandigarh","Ludhiana","Amritsar","Jalandhar","Patiala","Bathinda","Mohali","Pathankot","Hoshiarpur","Batala","Moga","Abohar","Malerkotla","Khanna","Phagwara","Firozpur","Kapurthala","Sangrur","Barnala","Faridkot","Muktsar","Fatehgarh Sahib","Rupnagar","Nawanshahr","Gurdaspur","Mansa","Tarn Taran"],
"Rajasthan":["Jaipur","Jodhpur","Kota","Bikaner","Ajmer","Udaipur","Bhilwara","Alwar","Bharatpur","Sikar","Pali","Sri Ganganagar","Hanumangarh","Tonk","Kishangarh","Beawar","Chittorgarh","Bundi","Baran","Jhalawar","Sawai Madhopur","Dausa","Dholpur","Karauli","Nagaur","Jhunjhunu","Churu","Jaisalmer","Barmer","Jalore","Sirohi","Pratapgarh","Banswara","Dungarpur","Rajsamand"],
"Sikkim":["Gangtok","Namchi","Gyalshing","Mangan","Pakyong","Soreng","Rangpo","Singtam"],
"Tamil Nadu":["Chennai","Coimbatore","Madurai","Tiruchirappalli","Salem","Tiruppur","Erode","Vellore","Thoothukudi","Dindigul","Thanjavur","Tirunelveli","Hosur","Nagercoil","Kanchipuram","Karur","Cuddalore","Kumbakonam","Tiruvannamalai","Pollachi","Rajapalayam","Sivakasi","Pudukkottai","Nagapattinam","Ramanathapuram","Sivaganga","Virudhunagar","Namakkal","Dharmapuri","Krishnagiri","Ariyalur","Perambalur","Nilgiris","Ooty","Tenkasi","Tirupattur","Ranipet","Mayiladuthurai"],
"Telangana":["Hyderabad","Warangal","Nizamabad","Karimnagar","Khammam","Ramagundam","Mahbubnagar","Nalgonda","Adilabad","Suryapet","Siddipet","Miryalaguda","Jagtial","Mancherial","Nirmal","Kamareddy","Sangareddy","Medak","Vikarabad","Wanaparthy","Nagarkurnool","Gadwal","Bhadradri Kothagudem","Mahabubabad","Jangaon","Bhupalpally","Asifabad"],
"Tripura":["Agartala","Udaipur","Dharmanagar","Kailasahar","Belonia","Khowai","Ambassa","Bishramganj"],
"Uttar Pradesh":["Lucknow","Kanpur","Ghaziabad","Agra","Varanasi","Prayagraj","Meerut","Bareilly","Aligarh","Moradabad","Saharanpur","Gorakhpur","Noida","Greater Noida","Firozabad","Jhansi","Muzaffarnagar","Mathura","Ayodhya","Faizabad","Rampur","Shahjahanpur","Farrukhabad","Mau","Hapur","Etawah","Mirzapur","Bulandshahr","Sambhal","Amroha","Hardoi","Fatehpur","Raebareli","Orai","Sitapur","Bahraich","Modinagar","Unnao","Jaunpur","Lakhimpur","Hathras","Banda","Pilibhit","Barabanki","Kushinagar","Deoria","Basti","Gonda","Balrampur","Sultanpur","Pratapgarh","Azamgarh","Ballia","Ghazipur","Chandauli","Sonbhadra","Bhadohi","Sant Kabir Nagar","Siddharthnagar","Maharajganj","Bijnor","Baghpat","Shamli","Kasganj","Etah","Mainpuri","Kannauj","Auraiya","Hamirpur","Mahoba","Chitrakoot","Kaushambi","Amethi","Ambedkar Nagar","Shravasti"],
"Uttarakhand":["Dehradun","Haridwar","Roorkee","Haldwani","Rudrapur","Kashipur","Rishikesh","Nainital","Almora","Pithoragarh","Bageshwar","Champawat","Pauri","Tehri","Uttarkashi","Chamoli","Rudraprayag"],
"West Bengal":["Kolkata","Howrah","Durgapur","Asansol","Siliguri","Bardhaman","Baharampur","Malda","Kharagpur","Haldia","Raiganj","Jalpaiguri","Cooch Behar","Bankura","Purulia","Krishnanagar","Barasat","Alipurduar","Balurghat","Suri","Tamluk","Jhargram","Chinsurah","Darjeeling","Kalimpong","Diamond Harbour"]
};
const states=Object.keys(DATA);
function ensureList(id){let el=d.getElementById(id);if(!el){el=d.createElement('datalist');el.id=id;d.body.appendChild(el)}return el}
function options(list){return list.map(x=>'<option value="'+String(x).replace(/"/g,'&quot;')+'"></option>').join('')}
function fillStates(){ensureList('suvidhaIndiaStateList').innerHTML=options(states)}
function fillCities(state){const list=DATA[state]||[];ensureList('suvidhaIndiaCityList').innerHTML=options(list)}
w.SUVIDHA_INDIA_LOCATIONS=DATA;
w.suvidhaIndiaStates=()=>states.slice();
w.suvidhaIndiaCities=state=>(DATA[String(state||'').trim()]||[]).slice();
w.suvidhaBindIndiaLocation=function(stateInput,cityInput){
 fillStates();if(!stateInput||!cityInput)return;
 const sync=()=>fillCities(stateInput.value);
 stateInput.setAttribute('list','suvidhaIndiaStateList');stateInput.setAttribute('autocomplete','off');
 cityInput.setAttribute('list','suvidhaIndiaCityList');cityInput.setAttribute('autocomplete','off');
 stateInput.addEventListener('input',sync);stateInput.addEventListener('change',sync);sync();
};
if(d.readyState==='loading')d.addEventListener('DOMContentLoaded',fillStates);else fillStates();
})(window,document);
