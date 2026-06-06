from flask import Flask, render_template, request
import csv
app = Flask(__name__)
print(__name__)


@app.route('/')
def my_home():
    return render_template('index.html')


@app.route('/<string:page_name>')
def dynamic_page(page_name):
    return render_template(f'{page_name}')

def write_to_csv(data):
    with open('database.csv', mode='a', newline='') as database2:
        email = data['email']
        subject = data['subject']
        message = data['message']
        csv_writer = csv.writer(database2, delimiter=',', quotechar='"', quoting=csv.QUOTE_MINIMAL)
        csv_writer.writerow([email, subject, message])


@app.route('/submit_form', methods=['POST', 'GET'])
def submit_form():
    if request.method == 'POST':
        try:
            data = request.form.to_dict()
            print(data)
        except Exception as e:
            print(e)
            return 'did not save to database'
        with open('database.txt', mode='a') as database:
            email = data['email']
            subject = data['subject']
            message = data['message']
            #file = database.write(f'\n{email}, {subject}, {message}')
            write_to_csv(data)
        return render_template('thankyou.html')
    else:
        return 'Something went wrong. Try again!'
    

# @app.route('/login', methods=['POST', 'GET'])
# def login():
#     error = None
#     if request.method == 'POST':
#         if valid_login(request.form['username'], request.form['password']):
#             return log_the_user_in(request.form['username'])
#         else:
#             error = 'Invalid username/password'

#     return render_template('login.html')
