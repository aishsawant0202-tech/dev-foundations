from flask import Flask, render_template, request
app = Flask(__name__)
print(__name__)

@app.route('/<username>/<int:post_id>')
def hello_world(username, post_id):
    return render_template('index.html', username=username, post_id=post_id)

@app.route('/about')
def about():
    return render_template('about.html')

# @app.route('/icon.png')
# def blog():
#     return render_template('icon.png')